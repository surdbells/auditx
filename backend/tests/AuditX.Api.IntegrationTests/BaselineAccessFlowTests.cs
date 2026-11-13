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

/// <summary>
/// The scenario the product owner posed, end to end through the API: several people who are AUDITORS on the platform
/// are named as the AUDITEES of one audit, alongside colleagues who are its auditors. On that audit the auditees
/// follow along and answer for their area — and not one auditor action is open to them.
///
/// (The seeded environment has four accounts, so here the audit has a lead, one auditor and TWO auditees who are
/// auditors by role; <c>AuditIndependenceTests</c> pins the same rules on the aggregate for the full two-auditors /
/// three-auditees arrangement.)
/// </summary>
public sealed class AuditorsNamedAsAuditeesFlowTests(ApiFactory factory) : AccessModelTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Auditors_named_as_auditees_of_an_audit_cannot_act_as_auditors_on_it()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var teamAuditor = users["auditor@auditx.local"];
        var auditeeOne = users["manager@auditx.local"];
        var auditeeTwo = users["auditee@auditx.local"];

        // All three hold the Auditor role: they are auditors on the platform.
        foreach (var auditor in new[] { teamAuditor, auditeeOne, auditeeTwo })
        {
            await GrantBuiltInRoleAsync(auditor, BuiltInRoles.AuditorName);
        }

        object NewAudit(Guid[] team, Guid[] otherAuditees) => new
        {
            name = $"Audit A {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["admin@auditx.local"], auditeeUserId = auditeeOne,
            additionalAuditeeUserIds = otherAuditees, teamMemberUserIds = team,
        };

        // Naming someone as BOTH an auditee and a team member is refused outright.
        var both = await admin.PostAsJsonAsync("/api/v1/audits", NewAudit(team: [teamAuditor, auditeeTwo], otherAuditees: [auditeeTwo]));
        Assert.Equal(HttpStatusCode.UnprocessableContent, both.StatusCode);
        Assert.Contains("auditee of this audit", await both.Content.ReadAsStringAsync());

        // Audit A: one auditor on the team, two auditees.
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", NewAudit(team: [teamAuditor], otherAuditees: [auditeeTwo])));
        var auditId = created.GetProperty("id").GetGuid();
        var roles = created.GetProperty("teamMembers").EnumerateArray()
            .ToDictionary(m => m.GetProperty("userId").GetGuid(), m => m.GetProperty("teamRole").GetString());
        Assert.Equal("auditee", roles[auditeeOne]);
        Assert.Equal("auditee", roles[auditeeTwo]);
        Assert.Equal("auditor", roles[teamAuditor]);

        // An auditee cannot be moved onto the audit team, nor a team member named an auditee — while it is still a draft…
        foreach (var auditee in new[] { auditeeOne, auditeeTwo })
        {
            foreach (var workingRole in new[] { "auditor", "reviewer" })
            {
                var cross = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
                    new { userId = auditee, teamRole = workingRole, version = await AuditVersionAsync(admin, auditId) });
                Assert.Equal(HttpStatusCode.UnprocessableContent, cross.StatusCode);
                Assert.Contains("auditee of this audit", await cross.Content.ReadAsStringAsync());
            }
        }

        var reverse = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = teamAuditor, teamRole = "auditee", version = await AuditVersionAsync(admin, auditId) });
        Assert.Equal(HttpStatusCode.UnprocessableContent, reverse.StatusCode);

        // …and once it has started nobody changes sides at all: the team is locked.
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = await AuditVersionAsync(admin, auditId) }));
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(withItem) }));
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        var afterStart = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = auditeeTwo, teamRole = "auditor", version = Version(started) });
        Assert.Equal(HttpStatusCode.Conflict, afterStart.StatusCode);

        // An item cannot be assigned to an auditee either.
        foreach (var auditee in new[] { auditeeOne, auditeeTwo })
        {
            var assign = await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/assignment",
                new { assigneeUserId = auditee, version = await AuditVersionAsync(admin, auditId) });
            Assert.Equal(HttpStatusCode.UnprocessableContent, assign.StatusCode);
        }

        // Each auditee signs in — an auditor by role — and tries the auditor's work on Audit A.
        foreach (var username in new[] { "manager", "auditee" })
        {
            var auditee = await LoginAsync(username);
            Assert.Contains("RespondItem", Permissions(await SessionAsync(auditee))); // they ARE auditors by role

            // They can follow the audit of their own area…
            var view = await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}"));
            var version = Version(view);

            // …and every auditor action on it is refused, with the reason.
            var attempts = new (string What, Func<Task<HttpResponseMessage>> Send)[]
            {
                ("answer an item", () => auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
                    new { verdict = "pass", comment = (string?)null, isDraft = false, version })),
                ("save a draft answer", () => auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
                    new { verdict = "pass", comment = "draft", isDraft = true, version })),
                ("raise a finding", () => auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
                {
                    checklistItemId = itemId, title = "Finding", severity = "low", rootCause = "x", recommendation = "y", ownerUserId = auditeeOne,
                })),
                ("record a procedure", () => auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/procedures", new
                {
                    type = "sampling", checklistItemId = itemId, performedOn = "2027-01-20", summary = "Tested 25 of 300 payment vouchers.",
                    population = 300, sampleSize = 25, itemsTested = 25, exceptionsFound = 0, method = "random",
                })),
                ("log audit time", () => auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/time-entries",
                    new { workDate = "2027-01-15", hours = 2m, category = "fieldwork", checklistItemId = (Guid?)null, notes = (string?)null })),
                ("request a document", () => auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests",
                    new { requestedFromUserId = teamAuditor, title = "Something", documentType = "approval_signoff", dueDate = "2027-01-25" })),
            };

            foreach (var (what, send) in attempts)
            {
                var response = await send();
                var body = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{username} could {what}: {(int)response.StatusCode} {body}");
                Assert.Contains("auditee of this audit", body);
            }
        }

        // Nothing they tried left a trace on the checklist…
        var summary = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/review/summary"));
        Assert.Equal(0, summary.GetProperty("responded").GetInt32());

        // …and the audit's actual auditor does the work.
        var auditorClient = await LoginAsync("auditor");
        var answered = await auditorClient.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = await AuditVersionAsync(auditorClient, auditId) });
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);

        // On an audit where they are NOT auditees, the same two people work as the auditors they are.
        var other = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Audit B {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["admin@auditx.local"], auditeeUserId = teamAuditor, teamMemberUserIds = new[] { auditeeOne, auditeeTwo },
        }));
        var otherId = other.GetProperty("id").GetGuid();
        var otherItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{otherId}/checklist/items",
            new { prompt = "Are limits monitored?", responseType = "pass_fail_na", isRequired = true, version = Version(other) }));
        var otherPlanned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{otherId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(otherItem) }));
        var otherStarted = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{otherId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(otherPlanned) }));
        var manager = await LoginAsync("manager");
        var worked = await manager.PostAsJsonAsync(
            $"/api/v1/audits/{otherId}/items/{otherStarted.GetProperty("checklistItems")[0].GetProperty("id").GetGuid()}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = Version(otherStarted) });
        Assert.Equal(HttpStatusCode.OK, worked.StatusCode);
    }
}
