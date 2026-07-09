using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P1-A enterprise risk register: register/assess/close lifecycle, likelihood×impact banding, register list,
/// permission gating, and the heatmap + register-summary analytics.
/// </summary>
public sealed class RiskFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grant the Audit Manager role (ViewAnalytics + View/ManageRisk) to 'manager' and log in.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    [Fact]
    public async Task Register_assess_band_and_close()
    {
        var admin = await LoginAsync("admin"); // Administrator: View + Manage risk
        var users = await UsersByEmailAsync(admin);
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // Register a Critical inherent risk (4×4 = 16).
        var risk = await DataAsync(await admin.PostAsJsonAsync("/api/v1/risks", new
        {
            title = $"Cyber intrusion {suffix}",
            category = "Technology",
            ownerUserId = users["manager@auditx.local"],
            inherentLikelihood = 4,
            inherentImpact = 4,
        }));
        var riskId = risk.GetProperty("id").GetGuid();
        Assert.Equal(16, risk.GetProperty("inherentScore").GetInt32());
        Assert.Equal("critical", risk.GetProperty("currentBand").GetString());
        Assert.Equal("open", risk.GetProperty("status").GetString());

        // Assess: residual 2×3 = 6 (Medium) with a mitigation → auto-advances to Assessed.
        var assessed = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/risks/{riskId}", new
        {
            title = $"Cyber intrusion {suffix}",
            category = "Technology",
            ownerUserId = users["manager@auditx.local"],
            inherentLikelihood = 4,
            inherentImpact = 4,
            residualLikelihood = 2,
            residualImpact = 3,
            treatmentStrategy = "mitigate",
            treatmentPlan = "Deploy EDR + MFA.",
            version = Version(risk),
        }));
        Assert.Equal(6, assessed.GetProperty("residualScore").GetInt32());
        Assert.Equal("medium", assessed.GetProperty("currentBand").GetString());
        Assert.Equal("assessed", assessed.GetProperty("status").GetString());
        Assert.Equal("mitigate", assessed.GetProperty("treatmentStrategy").GetString());

        // It appears in the register.
        var list = await DataAsync(await admin.GetAsync("/api/v1/risks?category=Technology&limit=100"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == riskId);

        // Close (rationale required) → terminal: a further edit is rejected.
        var closed = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/risks/{riskId}/transition", new
        {
            status = "closed", rationale = "Risk retired after remediation.", version = Version(assessed),
        }));
        Assert.Equal("closed", closed.GetProperty("status").GetString());

        var editClosed = await admin.PostAsJsonAsync($"/api/v1/risks/{riskId}/transition", new
        {
            status = "monitoring", rationale = (string?)null, version = Version(closed),
        });
        Assert.Equal(HttpStatusCode.Conflict, editClosed.StatusCode);
    }

    [Fact]
    public async Task Residual_cannot_exceed_inherent()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var risk = await DataAsync(await admin.PostAsJsonAsync("/api/v1/risks", new
        {
            title = $"Low risk {Guid.NewGuid():N}", category = "Ops",
            ownerUserId = users["auditor@auditx.local"], inherentLikelihood = 2, inherentImpact = 2,
        }));
        // Inherent 2×2 = 4; residual 3×3 = 9 would raise the risk → rejected (422).
        var bad = await admin.PatchAsJsonAsync($"/api/v1/risks/{risk.GetProperty("id").GetGuid()}", new
        {
            title = "x", category = "Ops", ownerUserId = users["auditor@auditx.local"],
            inherentLikelihood = 2, inherentImpact = 2, residualLikelihood = 3, residualImpact = 3,
            treatmentStrategy = "mitigate", version = Version(risk),
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);
    }

    [Fact]
    public async Task Managing_risks_requires_the_manage_risk_permission()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);

        var auditee = await LoginAsync("auditee"); // no ViewRisk / ManageRisk
        var resp = await auditee.PostAsJsonAsync("/api/v1/risks", new
        {
            title = "Nope", category = "Ops", ownerUserId = users["auditor@auditx.local"], inherentLikelihood = 3, inherentImpact = 3,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Heatmap_and_summary_reflect_open_risks()
    {
        var admin = await LoginAsync("admin");
        var analyst = await AnalystAsync(admin); // ViewAnalytics
        var users = await UsersByEmailAsync(admin);

        // A distinctive high-corner risk (5×5 = 25, Critical).
        await DataAsync(await admin.PostAsJsonAsync("/api/v1/risks", new
        {
            title = $"Extreme {Guid.NewGuid():N}", category = "Strategic",
            ownerUserId = users["manager@auditx.local"], inherentLikelihood = 5, inherentImpact = 5,
        }));

        var heatmap = await DataAsync(await analyst.GetAsync("/api/v1/analytics/risk-heatmap"));
        Assert.True(heatmap.GetProperty("totalOpen").GetInt32() >= 1);
        var cell = heatmap.GetProperty("cells").EnumerateArray()
            .First(c => c.GetProperty("likelihood").GetInt32() == 5 && c.GetProperty("impact").GetInt32() == 5);
        Assert.Equal("critical", cell.GetProperty("band").GetString());
        Assert.True(cell.GetProperty("count").GetInt32() >= 1);

        var summary = await DataAsync(await analyst.GetAsync("/api/v1/analytics/risk-summary"));
        Assert.True(summary.GetProperty("open").GetInt32() >= 1);
        Assert.Contains(summary.GetProperty("byBand").EnumerateArray(), b => b.GetProperty("key").GetString() == "critical");
    }
}
