using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Auditor capacity + workload: setting a user's capacity (person-days) and the analytics roll-up of open planned
/// effort (Planned / InProgress plan items) per assigned lead against that capacity — utilisation + over-commitment.
/// </summary>
public sealed class AuditorWorkloadFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grant Audit Manager to 'manager' (→ ViewAnalytics) and log in.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    [Fact]
    public async Task Set_capacity_then_workload_rolls_up_open_effort_against_it()
    {
        var admin = await LoginAsync("admin");        // Administrator → ManageUsers + universe/planning
        var analyst = await AnalystAsync(admin);      // Audit Manager → ViewAnalytics
        var leadId = (await UsersByEmailAsync(admin))["auditor@auditx.local"];

        // Set the lead's capacity to 10 person-days and confirm it round-trips through the detail read model.
        (await admin.PatchAsJsonAsync($"/api/v1/users/{leadId}/capacity", new { capacityDays = 10m })).EnsureSuccessStatusCode();
        var detail = await DataAsync(await admin.GetAsync($"/api/v1/users/{leadId}"));
        Assert.Equal(10m, detail.GetProperty("capacityDays").GetDecimal());

        // A plan with two open items led by that auditor: 8 + 5 = 13 planned days.
        var entity = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new { name = $"WL{Guid.NewGuid():N}", entityType = "process" }));
        var entityId = entity.GetProperty("id").GetGuid();
        var plan = await DataAsync(await admin.PostAsJsonAsync("/api/v1/annual-plans", new { periodLabel = "FY2098", periodStart = "2098-01-01", periodEnd = "2098-12-31" }));
        var planId = plan.GetProperty("id").GetGuid();
        foreach (var effort in new[] { 8, 5 })
        {
            (await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/items", new
            {
                entityIds = new[] { entityId },
                auditType = "process_review",
                plannedStartDate = "2098-03-01",
                plannedEndDate = "2098-03-31",
                estimatedEffortDays = effort,
                assignedLeadUserId = leadId,
            })).EnsureSuccessStatusCode();
        }

        // Workload scoped to this plan: the lead is over-committed (13 planned vs 10 capacity → 130%).
        var workload = await DataAsync(await analyst.GetAsync($"/api/v1/analytics/auditor-workload?annualPlanId={planId}"));
        var row = workload.EnumerateArray().First(w => w.GetProperty("leadUserId").GetGuid() == leadId);
        Assert.Equal(2, row.GetProperty("planItemCount").GetInt32());
        Assert.Equal(13m, row.GetProperty("plannedEffortDays").GetDecimal());
        Assert.Equal(10m, row.GetProperty("capacityDays").GetDecimal());
        Assert.Equal(130d, row.GetProperty("utilisationPercent").GetDouble());
        Assert.True(row.GetProperty("overCommitted").GetBoolean());

        // Clearing the capacity leaves the load visible but drops utilisation / over-commitment.
        (await admin.PatchAsJsonAsync($"/api/v1/users/{leadId}/capacity", new { capacityDays = (decimal?)null })).EnsureSuccessStatusCode();
        var cleared = await DataAsync(await analyst.GetAsync($"/api/v1/analytics/auditor-workload?annualPlanId={planId}"));
        var clearedRow = cleared.EnumerateArray().First(w => w.GetProperty("leadUserId").GetGuid() == leadId);
        Assert.Equal(13m, clearedRow.GetProperty("plannedEffortDays").GetDecimal());
        Assert.Equal(JsonValueKind.Null, clearedRow.GetProperty("capacityDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, clearedRow.GetProperty("utilisationPercent").ValueKind);
        Assert.False(clearedRow.GetProperty("overCommitted").GetBoolean());
    }

    [Fact]
    public async Task Setting_capacity_requires_manage_users()
    {
        var admin = await LoginAsync("admin");
        var leadId = (await UsersByEmailAsync(admin))["auditor@auditx.local"];

        var auditee = await LoginAsync("auditee"); // no ManageUsers
        var denied = await auditee.PatchAsJsonAsync($"/api/v1/users/{leadId}/capacity", new { capacityDays = 5m });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Capacity_beyond_a_year_is_rejected()
    {
        var admin = await LoginAsync("admin");
        var leadId = (await UsersByEmailAsync(admin))["auditor@auditx.local"];

        var bad = await admin.PatchAsJsonAsync($"/api/v1/users/{leadId}/capacity", new { capacityDays = 500m });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);
    }
}
