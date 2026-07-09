using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P0-B time/effort capture: logging, amending, deleting time against an audit; the per-audit budget-vs-actual
/// summary; permission gating; and the cross-audit budget/utilisation analytics.
/// </summary>
public sealed class TimeEntryFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grant the Audit Manager role (ViewAnalytics + LogTime + ManageAudit) to 'manager' and log in.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    /// <summary>An in-progress audit (loggable) led by 'manager'.</summary>
    private async Task<Guid> SeedInProgressAuditAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Time {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        // Planning requires at least one checklist item AND at least one auditor on the team.
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(withItem);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);

        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        return auditId;
    }

    [Fact]
    public async Task Log_amend_delete_and_budget_summary()
    {
        var admin = await LoginAsync("admin");
        var auditId = await SeedInProgressAuditAsync(admin);

        // A manager sets a 40-hour budget.
        var budgeted = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}/budget",
            new { budgetedHours = 40m, version = await AuditVersionAsync(admin, auditId) }));
        Assert.Equal(40m, budgeted.GetProperty("budgetedHours").GetDecimal());

        // Log 6 hours of fieldwork.
        var entry = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/time-entries",
            new { workDate = "2027-01-15", hours = 6m, category = "fieldwork", checklistItemId = (Guid?)null, notes = "reviewed controls" }));
        Assert.Equal(6m, entry.GetProperty("hours").GetDecimal());
        Assert.Equal("fieldwork", entry.GetProperty("category").GetString());
        var entryId = entry.GetProperty("id").GetGuid();
        var entryVersion = Version(entry);

        // Summary: 6 of 40 → 34 remaining, one entry.
        var summary = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/time-entries/summary"));
        Assert.Equal(6m, summary.GetProperty("actualHours").GetDecimal());
        Assert.Equal(40m, summary.GetProperty("budgetedHours").GetDecimal());
        Assert.Equal(34m, summary.GetProperty("varianceHours").GetDecimal());
        Assert.Equal(1, summary.GetProperty("entryCount").GetInt32());

        // Amend to 8 hours / review.
        var amended = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/time-entries/{entryId}",
            new { workDate = "2027-01-15", hours = 8m, category = "review", checklistItemId = (Guid?)null, notes = (string?)null, version = entryVersion }));
        Assert.Equal(8m, amended.GetProperty("hours").GetDecimal());
        Assert.Equal("review", amended.GetProperty("category").GetString());
        var amendedVersion = Version(amended);

        // Delete it (soft) → summary back to zero.
        var del = await admin.DeleteAsync($"/api/v1/time-entries/{entryId}?version={Uri.EscapeDataString(amendedVersion)}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var after = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/time-entries/summary"));
        Assert.Equal(0m, after.GetProperty("actualHours").GetDecimal());
        Assert.Equal(0, after.GetProperty("entryCount").GetInt32());
    }

    [Fact]
    public async Task Logging_requires_the_log_time_permission()
    {
        var admin = await LoginAsync("admin");
        var auditId = await SeedInProgressAuditAsync(admin);

        var auditee = await LoginAsync("auditee"); // no LogTime permission
        var resp = await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/time-entries",
            new { workDate = "2027-01-15", hours = 2m, category = "fieldwork", checklistItemId = (Guid?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Budget_and_utilisation_analytics_include_logged_time()
    {
        var admin = await LoginAsync("admin");
        var auditId = await SeedInProgressAuditAsync(admin);
        var analyst = await AnalystAsync(admin); // manager: ViewAnalytics + LogTime + lead (team member)

        await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}/budget",
            new { budgetedHours = 20m, version = await AuditVersionAsync(admin, auditId) }));
        await DataAsync(await analyst.PostAsJsonAsync($"/api/v1/audits/{auditId}/time-entries",
            new { workDate = "2027-01-16", hours = 5m, category = "fieldwork", checklistItemId = (Guid?)null, notes = (string?)null }));

        // Budget-vs-actual lists this audit with 5 of 20 hours (75% remaining).
        var bva = await DataAsync(await analyst.GetAsync("/api/v1/analytics/budget-vs-actual"));
        var row = bva.EnumerateArray().Single(r => r.GetProperty("auditId").GetGuid() == auditId);
        Assert.Equal(20m, row.GetProperty("budgetedHours").GetDecimal());
        Assert.Equal(5m, row.GetProperty("actualHours").GetDecimal());
        Assert.Equal(15m, row.GetProperty("varianceHours").GetDecimal());

        // Utilisation attributes the 5 hours to the manager.
        var users = await UsersByEmailAsync(admin);
        var util = await DataAsync(await analyst.GetAsync("/api/v1/analytics/utilisation"));
        var mine = util.EnumerateArray().Single(u => u.GetProperty("userId").GetGuid() == users["manager@auditx.local"]);
        Assert.True(mine.GetProperty("totalHours").GetDecimal() >= 5m);
        Assert.True(mine.GetProperty("byCategory").EnumerateArray().Any(c => c.GetProperty("category").GetString() == "fieldwork"));
    }

    [Fact]
    public async Task Auditee_cannot_view_the_teams_time_or_budget()
    {
        var admin = await LoginAsync("admin");
        var auditId = await SeedInProgressAuditAsync(admin);
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/time-entries",
            new { workDate = "2027-01-15", hours = 3m, category = "fieldwork", checklistItemId = (Guid?)null, notes = (string?)null }));

        // The auditee is a permanent team member and holds ViewAudit, but must NOT see the audit team's
        // effort/budget/per-auditor utilisation (they lack ViewTimeEntries and are TeamRole.Auditee).
        var auditee = await LoginAsync("auditee");
        var list = await auditee.GetAsync($"/api/v1/audits/{auditId}/time-entries");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        var summary = await auditee.GetAsync($"/api/v1/audits/{auditId}/time-entries/summary");
        Assert.Equal(HttpStatusCode.Forbidden, summary.StatusCode);
    }

    [Fact]
    public async Task Cannot_log_against_a_draft_audit()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        // A freshly-created audit is in Draft (not launched) → not loggable.
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Draft {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();

        var resp = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/time-entries",
            new { workDate = "2027-01-15", hours = 2m, category = "fieldwork", checklistItemId = (Guid?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }
}
