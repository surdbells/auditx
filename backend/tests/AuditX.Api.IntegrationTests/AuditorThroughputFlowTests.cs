using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Non-lead auditor throughput analytics: per-user hands-on work (finalised responses, evidence, findings raised,
/// items assigned), the PerformanceAnalyticsView gate, and self-coverage suppression unless the caller holds CIA.
/// </summary>
public sealed class AuditorThroughputFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

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

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray().ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    private async Task<Guid> GrantAuditManagerAsync(HttpClient admin, string email)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users[email]}/roles", new { roleId, scopeValue = (string?)null });
        return users[email];
    }

    private async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    /// <summary>Seed an in-progress audit: one item assigned to the manager, responded (Fail) by admin, with a finding raised by admin.</summary>
    private async Task<(Guid ManagerId, Guid AdminId)> SeedAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var managerId = users["manager@auditx.local"];
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"TP {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = managerId, auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        // Item assigned to the manager (the lead is an active team member) → manager accrues itemsAssigned.
        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, assignedUserId = managerId, version }));
        version = Version(a1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var item = started.GetProperty("checklistItems").EnumerateArray().First().GetProperty("id").GetGuid();

        // admin finalises the response (Fail) and raises a finding → admin accrues itemsResponded + exceptionsRaised.
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = item, title = "Gap", severity = "low", rootCause = "rc", recommendation = "rec", ownerUserId = users["auditee@auditx.local"],
        }));
        return (managerId, users["admin@auditx.local"]);
    }

    [Fact]
    public async Task Throughput_counts_hands_on_work_gates_on_performance_view_and_suppresses_self()
    {
        var admin = await LoginAsync("admin");
        var (managerId, adminId) = await SeedAsync(admin);

        // No PerformanceAnalyticsView → forbidden.
        var auditee = await LoginAsync("auditee");
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync("/api/v1/analytics/auditor-throughput")).StatusCode);

        // The analyst (manager: PerformanceAnalyticsView, not CIA) sees admin's hands-on work…
        await GrantAuditManagerAsync(admin, "manager@auditx.local");
        var analyst = await LoginAsync("manager");
        var rows = await DataAsync(await analyst.GetAsync("/api/v1/analytics/auditor-throughput"));
        var adminRow = rows.EnumerateArray().First(r => r.GetProperty("userId").GetGuid() == adminId);
        Assert.True(adminRow.GetProperty("itemsResponded").GetInt32() >= 1);
        Assert.True(adminRow.GetProperty("exceptionsRaised").GetInt32() >= 1);

        // …but NOT their own row, even though the manager holds an assigned item (self-coverage suppression, no CIA).
        Assert.DoesNotContain(rows.EnumerateArray(), r => r.GetProperty("userId").GetGuid() == managerId);
    }

    [Fact]
    public async Task A_cia_holder_sees_their_own_throughput_row()
    {
        var admin = await LoginAsync("admin"); // Administrator holds CIA
        var (_, adminId) = await SeedAsync(admin);

        // Grant admin the Audit Manager role too → PerformanceAnalyticsView (admin already holds CIA); re-login for it.
        await GrantAuditManagerAsync(admin, "admin@auditx.local");
        var elevated = await LoginAsync("admin");

        var rows = await DataAsync(await elevated.GetAsync("/api/v1/analytics/auditor-throughput"));
        // admin has throughput AND holds CIA → their own row is included.
        Assert.Contains(rows.EnumerateArray(), r => r.GetProperty("userId").GetGuid() == adminId);
    }
}
