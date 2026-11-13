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

/// <summary>
/// Data isolation for a caller WITHOUT portfolio visibility — the Auditee, who holds ViewAudit / ViewExceptions but
/// not ViewAudits. Holding those permissions must only ever open what the caller is a party to: an audit they are on,
/// a finding they own or whose audit they are on, a root-cause gap they answer for. Other areas' audits, finding
/// titles and gaps stay invisible, whether asked for by id, through the header search or through the gap register.
/// Callers with portfolio visibility (Auditor / Audit Manager / Administrator) keep the whole picture.
/// </summary>
public sealed class AuditeeDataIsolationFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = NewClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Grants a built-in role directly (test setup) — dev users other than admin are provisioned role-less.</summary>
    private async Task GrantBuiltInRoleAsync(Guid userId, string roleName)
    {
        using var scope = factory.Services.CreateScope();
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

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    private sealed record SeededAudit(Guid AuditId, Guid ItemId, Guid FindingId, string FindingTitle);

    /// <summary>
    /// An in-progress audit of <paramref name="auditeeEmail"/>'s area: lead = manager, working team = admin (who
    /// answers the one item as Fail), with one finding raised on it and owned by the auditee. Nobody else is on it.
    /// </summary>
    private async Task<SeededAudit> SeedAuditWithFindingAsync(HttpClient admin, Dictionary<string, Guid> users, string auditeeEmail, string findingTitle)
    {
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Isolation {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users[auditeeEmail],
        }));
        var auditId = created.GetProperty("id").GetGuid();

        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = Version(created) }));
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["admin@auditx.local"], teamRole = "auditor", version = Version(withItem) }));
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(withTeam) }));
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "fail", comment = "No maker-checker on payments.", isDraft = false, version = Version(started) }));
        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = itemId, title = findingTitle, severity = "high",
            rootCause = "x", recommendation = "y", ownerUserId = users[auditeeEmail],
        }));

        return new SeededAudit(auditId, itemId, raised.GetProperty("id").GetGuid(), findingTitle);
    }

    /// <summary>Every read endpoint that exposes one audit's content behind the ViewAudit permission.</summary>
    private static string[] AuditReadPaths(SeededAudit a) =>
    [
        $"/api/v1/audits/{a.AuditId}",
        $"/api/v1/audits/{a.AuditId}/history",
        $"/api/v1/audits/{a.AuditId}/team",
        $"/api/v1/audits/{a.AuditId}/checklist",
        $"/api/v1/audits/{a.AuditId}/checklist/progress",
        $"/api/v1/audits/{a.AuditId}/items/{a.ItemId}/responses",
        $"/api/v1/audits/{a.AuditId}/items/{a.ItemId}/responses/history",
        $"/api/v1/audits/{a.AuditId}/items/{a.ItemId}/response-input",
        $"/api/v1/audits/{a.AuditId}/review/summary",
        $"/api/v1/audits/{a.AuditId}/review/fail-without-exception",
    ];

    [Fact]
    public async Task An_auditee_can_open_the_audit_of_their_own_area_and_no_other()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var mine = await SeedAuditWithFindingAsync(admin, users, "auditee@auditx.local", $"Mine {Guid.NewGuid():N}");
        var theirs = await SeedAuditWithFindingAsync(admin, users, "auditor@auditx.local", $"Theirs {Guid.NewGuid():N}");

        await GrantBuiltInRoleAsync(users["auditee@auditx.local"], BuiltInRoles.AuditeeName);
        var auditee = await LoginAsync("auditee");

        // The audit of their own area: everything opens.
        foreach (var path in AuditReadPaths(mine))
        {
            var response = await auditee.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"auditee should read {path} but got {(int)response.StatusCode}");
        }

        // Another area's audit: nothing opens, even with the id in hand — not the detail, the checklist, the answers,
        // the review figures or the history.
        foreach (var path in AuditReadPaths(theirs))
        {
            var response = await auditee.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"auditee must not read {path} but got {(int)response.StatusCode}");
        }

        // Portfolio visibility is unchanged: an Auditor who is NOT on the first audit's team still opens it (they can
        // already list every audit), and the administrator opens both.
        await GrantBuiltInRoleAsync(users["auditor@auditx.local"], BuiltInRoles.AuditorName);
        var auditor = await LoginAsync("auditor");
        foreach (var path in AuditReadPaths(mine))
        {
            var response = await auditor.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"an auditor should read {path} but got {(int)response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/audits/{mine.AuditId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/audits/{theirs.AuditId}")).StatusCode);
    }

    [Fact]
    public async Task The_header_search_only_finds_findings_the_caller_is_a_party_to()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var token = $"zq{Guid.NewGuid():N}"[..12]; // a term only these two findings contain
        var mine = await SeedAuditWithFindingAsync(admin, users, "auditee@auditx.local", $"{token} segregation gap in my area");
        var theirs = await SeedAuditWithFindingAsync(admin, users, "auditor@auditx.local", $"{token} treasury limit breach elsewhere");

        static Guid[] ExceptionHits(JsonElement results) => results.GetProperty("hits").EnumerateArray()
            .Where(h => h.GetProperty("type").GetString() == "exception")
            .Select(h => Guid.Parse(h.GetProperty("id").GetString()!))
            .ToArray();

        await GrantBuiltInRoleAsync(users["auditee@auditx.local"], BuiltInRoles.AuditeeName);
        var auditee = await LoginAsync("auditee");

        // The auditee finds their own finding — and not the other area's, whose title would otherwise leak.
        var auditeeHits = ExceptionHits(await DataAsync(await auditee.GetAsync($"/api/v1/search?q={token}")));
        Assert.Contains(mine.FindingId, auditeeHits);
        Assert.DoesNotContain(theirs.FindingId, auditeeHits);

        // Portfolio visibility finds both.
        var adminHits = ExceptionHits(await DataAsync(await admin.GetAsync($"/api/v1/search?q={token}")));
        Assert.Contains(mine.FindingId, adminHits);
        Assert.Contains(theirs.FindingId, adminHits);
    }

    [Fact]
    public async Task The_root_cause_gap_register_shows_an_auditee_only_the_gaps_they_answer_for()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var mine = await SeedAuditWithFindingAsync(admin, users, "auditee@auditx.local", $"Mine {Guid.NewGuid():N}");
        var theirs = await SeedAuditWithFindingAsync(admin, users, "auditor@auditx.local", $"Theirs {Guid.NewGuid():N}");

        async Task<Guid> OpenGapAsync(string title, string ownerEmail, params Guid[] findingIds)
        {
            var gap = await DataAsync(await admin.PostAsJsonAsync("/api/v1/root-cause-gaps", new
            {
                title, description = "Recurring across areas", category = "process_gap",
                ownerUserId = users[ownerEmail], targetDate = "2027-06-30",
            }));
            var gapId = gap.GetProperty("id").GetGuid();
            foreach (var findingId in findingIds)
            {
                gap = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}/exceptions",
                    new { exceptionId = findingId, version = Version(gap) }));
            }

            return gapId;
        }

        // A gap the auditee OWNS, fed by a finding of their own area and one from another area…
        var ownedGap = await OpenGapAsync($"Owned by the auditee {Guid.NewGuid():N}", "auditee@auditx.local", mine.FindingId, theirs.FindingId);
        // …and a gap that has nothing to do with them.
        var otherGap = await OpenGapAsync($"Someone else's gap {Guid.NewGuid():N}", "manager@auditx.local", theirs.FindingId);

        static Guid[] Ids(JsonElement paged) => paged.GetProperty("items").EnumerateArray().Select(g => g.GetProperty("id").GetGuid()).ToArray();
        static Guid[] LinkedFindings(JsonElement gap) =>
            gap.GetProperty("linkedExceptions").EnumerateArray().Select(e => e.GetProperty("exceptionId").GetGuid()).ToArray();

        await GrantBuiltInRoleAsync(users["auditee@auditx.local"], BuiltInRoles.AuditeeName);
        var auditee = await LoginAsync("auditee");

        // The register lists only the gap they answer for.
        var auditeeRegister = Ids(await DataAsync(await auditee.GetAsync("/api/v1/root-cause-gaps?pageSize=500")));
        Assert.Contains(ownedGap, auditeeRegister);
        Assert.DoesNotContain(otherGap, auditeeRegister);

        // Someone else's gap does not open by id either.
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync($"/api/v1/root-cause-gaps/{otherGap}")).StatusCode);

        // Their own gap opens — showing only the linked finding they could open themselves, not the other area's.
        var ownedAsAuditee = LinkedFindings(await DataAsync(await auditee.GetAsync($"/api/v1/root-cause-gaps/{ownedGap}")));
        Assert.Contains(mine.FindingId, ownedAsAuditee);
        Assert.DoesNotContain(theirs.FindingId, ownedAsAuditee);

        // Portfolio visibility: the whole register and every linked finding.
        var adminRegister = Ids(await DataAsync(await admin.GetAsync("/api/v1/root-cause-gaps?pageSize=500")));
        Assert.Contains(ownedGap, adminRegister);
        Assert.Contains(otherGap, adminRegister);
        var ownedAsAdmin = LinkedFindings(await DataAsync(await admin.GetAsync($"/api/v1/root-cause-gaps/{ownedGap}")));
        Assert.Contains(mine.FindingId, ownedAsAdmin);
        Assert.Contains(theirs.FindingId, ownedAsAdmin);
    }
}
