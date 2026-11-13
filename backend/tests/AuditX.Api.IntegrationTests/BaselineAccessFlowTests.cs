using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Domain.Authorization;
using AuditX.Domain.Identity;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

/// <summary>Shared plumbing for the access-model tests below (each class has its own database).</summary>
public abstract class AccessModelTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    protected async Task<HttpClient> LoginAsync(string username)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    protected static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} from {response.RequestMessage?.RequestUri?.PathAndQuery}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    protected static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    protected static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grants a built-in role directly (test setup) — dev users other than admin are provisioned role-less.</summary>
    protected async Task GrantBuiltInRoleAsync(Guid userId, string roleName)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.FirstAsync(r => r.Name == roleName);
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id))
        {
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            db.UserRoles.Add(UserRole.Grant(userId, role.Id));
            user.MarkActiveOnFirstRole();
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IPermissionResolver>().InvalidateAsync(userId);
        }
    }

    protected static async Task<JsonElement> SessionAsync(HttpClient client)
        => await DataAsync(await client.GetAsync("/api/v1/auth/session"));

    protected static string[] Permissions(JsonElement session)
        => session.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToArray();

    protected sealed record SeededAudit(Guid AuditId, Guid ItemId);

    /// <summary>
    /// An in-progress audit with one item. <paramref name="actor"/> (who must hold ManageAudit) creates it; the lead is
    /// on the team by definition, and <paramref name="auditorEmail"/> joins as the audit's auditor.
    /// </summary>
    protected static async Task<SeededAudit> SeedStartedAuditAsync(
        HttpClient actor, Dictionary<string, Guid> users, string leadEmail, string auditeeEmail, string auditorEmail)
    {
        var created = await DataAsync(await actor.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Access {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users[leadEmail], auditeeUserId = users[auditeeEmail],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var withItem = await DataAsync(await actor.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = Version(created) }));
        var withTeam = await DataAsync(await actor.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users[auditorEmail], teamRole = "auditor", version = Version(withItem) }));
        var planned = await DataAsync(await actor.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(withTeam) }));
        var started = await DataAsync(await actor.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) }));
        return new SeededAudit(auditId, started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid());
    }

    protected static async Task<string> AuditVersionAsync(HttpClient client, Guid auditId)
        => Version(await DataAsync(await client.GetAsync($"/api/v1/audits/{auditId}")));

    /// <summary>The team member answers the item Fail and a finding is raised on it for <paramref name="ownerId"/>.</summary>
    protected static async Task<JsonElement> FailAndRaiseAsync(HttpClient teamMember, SeededAudit audit, Guid ownerId)
    {
        await DataAsync(await teamMember.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/items/{audit.ItemId}/responses",
            new { verdict = "fail", comment = "No maker-checker on payments.", isDraft = false, version = await AuditVersionAsync(teamMember, audit.AuditId) }));
        return await DataAsync(await teamMember.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/exceptions", new
        {
            checklistItemId = audit.ItemId, title = $"Segregation gap {Guid.NewGuid():N}", severity = "medium",
            rootCause = "x", recommendation = "y", ownerUserId = ownerId,
        }));
    }

    protected static object MapBody(Guid ownerId, string version) => new
    {
        actions = new[]
        {
            new
            {
                description = "Introduce dual control", ownerUserId = ownerId,
                targetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10).ToString("yyyy-MM-dd"), expectedEvidenceType = "screenshot",
            },
        },
        version,
    };
}

/// <summary>
/// The sign-in gate under the "auditee is a relationship" model: a person with no role who is named on nothing has no
/// access ("Access pending"); the moment the platform names them on something they are in — with auditee abilities,
/// limited to what they are named on — and no administrator has to assign anything.
/// </summary>
public sealed class BaselineAccessGateTests(ApiFactory factory) : AccessModelTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_person_gets_in_once_named_on_an_audit_and_sees_only_what_they_are_named_on()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);

        // No role, named on nothing: no permissions at all — the client keeps them on "Access pending".
        var auditee = await LoginAsync("auditee");
        var before = await SessionAsync(auditee);
        Assert.Equal("awaiting_role_assignment", before.GetProperty("status").GetString());
        Assert.Empty(Permissions(before));
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync("/api/v1/exceptions")).StatusCode);

        // An audit of their area names them as its auditee. Nobody assigns them a role.
        var mine = await SeedStartedAuditAsync(admin, users, "manager@auditx.local", "auditee@auditx.local", "admin@auditx.local");
        var theirs = await SeedStartedAuditAsync(admin, users, "manager@auditx.local", "auditor@auditx.local", "admin@auditx.local");

        // Their very next request sees it: active, holding exactly the auditee abilities, and still no role.
        var after = await SessionAsync(auditee);
        Assert.Equal("active", after.GetProperty("status").GetString());
        Assert.Equal(BaselineAccess.Permissions.OrderBy(p => p), Permissions(after).OrderBy(p => p));
        Assert.Empty(after.GetProperty("roles").EnumerateArray());

        // Limited to what they are named on: their own audit opens, another area's does not; their worklists answer.
        Assert.Equal(HttpStatusCode.OK, (await auditee.GetAsync($"/api/v1/audits/{mine.AuditId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync($"/api/v1/audits/{theirs.AuditId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditee.GetAsync("/api/v1/exceptions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditee.GetAsync("/api/v1/my/evidence-requests")).StatusCode);

        // The baseline is the auditee's abilities and nothing more: no register, no audit-side action.
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync("/api/v1/audits")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync("/api/v1/users")).StatusCode);
    }

    [Fact]
    public async Task The_auditee_role_is_no_longer_handed_out()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var auditeeRole = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == BuiltInRoles.AuditeeName);
        var auditorRole = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == BuiltInRoles.AuditorName);

        // The role list marks it so pickers leave it out; every other role stays assignable.
        Assert.False(auditeeRole.GetProperty("isAssignable").GetBoolean());
        Assert.True(auditorRole.GetProperty("isAssignable").GetBoolean());

        // Granting it is refused, with the reason.
        var grant = await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles",
            new { roleId = auditeeRole.GetProperty("id").GetGuid(), scopeValue = (string?)null });
        Assert.Equal(HttpStatusCode.UnprocessableContent, grant.StatusCode);
        Assert.Contains("automatically", await grant.Content.ReadAsStringAsync());
    }
}

/// <summary>
/// Independence: someone who holds auditor or manager powers everywhere is still only the AUDITEE on the audit of
/// their own area. They can read it and do the auditee's part; they cannot run it, change it, judge its findings,
/// approve their own action plan or issue its report.
/// </summary>
public sealed class AuditeeIndependenceFlowTests(ApiFactory factory) : AccessModelTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task An_audit_manager_who_is_the_auditee_cannot_run_the_audit_of_their_own_area()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        await GrantBuiltInRoleAsync(users["manager@auditx.local"], BuiltInRoles.AuditManagerName);

        // The audit manager's own area is audited: admin leads it, the manager is its auditee.
        var audit = await SeedStartedAuditAsync(admin, users, "admin@auditx.local", "manager@auditx.local", "auditor@auditx.local");
        var manager = await LoginAsync("manager");

        // They can read it — it is about them…
        var read = await DataAsync(await manager.GetAsync($"/api/v1/audits/{audit.AuditId}"));
        var version = Version(read);

        // …but none of their manager powers apply to it.
        async Task AssertRefusedAsync(HttpResponseMessage response, string what)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{what}: expected 403 but got {(int)response.StatusCode}: {body}");
            Assert.Contains("auditee of this audit", body);
        }

        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/transition",
            new { targetState = "under_review", reason = "Wrapping up my own audit.", version }), "transition");
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/checklist/items",
            new { prompt = "An easier question", responseType = "pass_fail_na", isRequired = false, version }), "add checklist item");
        await AssertRefusedAsync(await manager.PatchAsJsonAsync($"/api/v1/audits/{audit.AuditId}/budget",
            new { budgetedHours = 1, version }), "set budget");
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/cancel",
            new { reason = "I would rather not be audited, thank you.", version }), "cancel");
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/items/{audit.ItemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version }), "answer an item");

        // The lead finds a gap and raises it against the manager.
        var finding = await FailAndRaiseAsync(admin, audit, users["manager@auditx.local"]);
        var findingId = finding.GetProperty("id").GetGuid();

        // Audit-side actions on the finding: refused.
        await AssertRefusedAsync(await manager.PatchAsJsonAsync($"/api/v1/exceptions/{findingId}/severity",
            new { severity = "low", reason = "It is really not that serious.", version = Version(finding) }), "downgrade severity");
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/cancel",
            new { reason = "Raised in error, in my opinion.", version = Version(finding) }), "cancel the finding");
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/exceptions", new
        {
            checklistItemId = audit.ItemId, title = "Another", severity = "low", rootCause = "x", recommendation = "y", ownerUserId = users["manager@auditx.local"],
        }), "raise a finding");

        // The auditee's part is open to them: as the finding's owner they submit its action plan…
        var submitted = await DataAsync(await manager.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/map",
            MapBody(users["manager@auditx.local"], Version(finding))));
        Assert.Equal("map_submitted", submitted.GetProperty("status").GetString());

        // …and cannot then approve it themselves, nor issue the report on their own audit.
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/map/approve",
            new { version = Version(submitted) }), "approve own action plan");
        await AssertRefusedAsync(await manager.PostAsJsonAsync($"/api/v1/audits/{audit.AuditId}/reports",
            new { formats = new[] { "html" } }), "generate the report");

        // On an audit that is NOT about them, the same manager's powers are intact.
        var other = await SeedStartedAuditAsync(admin, users, "admin@auditx.local", "auditee@auditx.local", "auditor@auditx.local");
        var otherVersion = await AuditVersionAsync(manager, other.AuditId);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync($"/api/v1/audits/{other.AuditId}/budget",
            new { budgetedHours = 40, version = otherVersion })).StatusCode);
    }
}

/// <summary>
/// The people a finding NAMES act on it: its owner opens it and writes its action plan even when they are not on the
/// audit's team — and being on the team is not enough to write management's plan for them.
/// </summary>
public sealed class FindingOwnerAccessFlowTests(ApiFactory factory) : AccessModelTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_findings_owner_outside_the_audit_can_open_it_and_only_the_owner_writes_its_action_plan()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        await GrantBuiltInRoleAsync(users["auditor@auditx.local"], BuiltInRoles.AuditorName);

        // Admin leads; the auditor is on the team; the finding is owned by the manager, who is NOT on the audit and
        // holds no role at all.
        var audit = await SeedStartedAuditAsync(admin, users, "admin@auditx.local", "auditee@auditx.local", "auditor@auditx.local");
        var ownerId = users["manager@auditx.local"];
        var owner = await LoginAsync("manager");
        Assert.Empty(Permissions(await SessionAsync(owner)));

        var finding = await FailAndRaiseAsync(admin, audit, ownerId);
        var findingId = finding.GetProperty("id").GetGuid();

        // Being named as the finding's owner is what lets them in.
        Assert.Equal("active", (await SessionAsync(owner)).GetProperty("status").GetString());

        // They find it in their tracker and can open it, with its history — though not the audit it came from.
        var tracker = await DataAsync(await owner.GetAsync("/api/v1/exceptions?pageSize=100"));
        Assert.Contains(tracker.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == findingId);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/exceptions/{findingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/exceptions/{findingId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/v1/audits/{audit.AuditId}")).StatusCode);

        // A team auditor holds SubmitMap by role, but the action plan is management's to write — not theirs.
        var auditor = await LoginAsync("auditor");
        var byAuditor = await auditor.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/map", MapBody(ownerId, Version(finding)));
        Assert.Equal(HttpStatusCode.Forbidden, byAuditor.StatusCode);

        // The owner writes it.
        var submitted = await DataAsync(await owner.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/map", MapBody(ownerId, Version(finding))));
        Assert.Equal("map_submitted", submitted.GetProperty("status").GetString());

        // Owning the finding does not make them an auditor of it.
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/map/approve",
            new { version = Version(submitted) })).StatusCode);
    }
}
