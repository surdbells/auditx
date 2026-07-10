using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Analytics.Services;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

public sealed class AnalyticsFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>Grant the Audit Manager role to 'manager' (best-effort, idempotent) → ViewAnalytics + PerformanceAnalyticsView, no Cia.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        var managerId = users.GetProperty("items").EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "manager@auditx.local").GetProperty("id").GetGuid();
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{managerId}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    private static IReadOnlyList<string> Slugs(JsonElement listData) =>
        listData.EnumerateArray().Select(d => d.GetProperty("slug").GetString()!).ToArray();

    [Fact]
    public async Task Seed_creates_the_six_default_dashboards()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Dashboards.IgnoreQueryFilters().CountAsync();
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task A_roleless_user_sees_only_ungated_dashboards()
    {
        var auditee = await LoginAsync("auditee"); // no role → no Cia, no PerformanceAnalyticsView
        var slugs = Slugs(await DataAsync(await auditee.GetAsync("/api/v1/dashboards")));

        Assert.Contains("exception_portfolio", slugs);      // ungated
        Assert.DoesNotContain("sanctions_consistency", slugs); // Cia
        Assert.DoesNotContain("function_performance", slugs);  // PerformanceAnalyticsView
    }

    [Fact]
    public async Task Gated_dashboards_appear_for_holders_of_their_permission()
    {
        var admin = await LoginAsync("admin");                  // holds Cia, not PerformanceAnalyticsView
        var adminSlugs = Slugs(await DataAsync(await admin.GetAsync("/api/v1/dashboards")));
        Assert.Contains("sanctions_consistency", adminSlugs);
        Assert.DoesNotContain("function_performance", adminSlugs);

        var analyst = await AnalystAsync(admin);                // holds PerformanceAnalyticsView, not Cia
        var analystSlugs = Slugs(await DataAsync(await analyst.GetAsync("/api/v1/dashboards")));
        Assert.Contains("function_performance", analystSlugs);
        Assert.DoesNotContain("sanctions_consistency", analystSlugs);
    }

    [Fact]
    public async Task Performance_scorecards_are_gated_by_the_view_permission()
    {
        var auditee = await LoginAsync("auditee");
        var denied = await auditee.GetAsync("/api/v1/analytics/performance-scorecards");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var analyst = await AnalystAsync(await LoginAsync("admin"));
        var allowed = await analyst.GetAsync("/api/v1/analytics/performance-scorecards");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Analytics_endpoints_require_view_analytics()
    {
        var auditee = await LoginAsync("auditee"); // no ViewAnalytics
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync("/api/v1/analytics/function-performance")).StatusCode);
    }

    [Fact]
    public async Task Sanctions_consistency_analytics_never_exposes_subject_identity()
    {
        var analyst = await AnalystAsync(await LoginAsync("admin"));
        var response = await analyst.GetAsync("/api/v1/analytics/sanctions-consistency");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();

        // The projection physically omits the subject — assert no identifying field leaked into the payload.
        Assert.DoesNotContain("subject", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recurrence_scan_runs_against_real_sql_and_the_list_endpoint_responds()
    {
        // No closed exceptions are seeded → the scan resolves its full graph + runs with zero detections (proves wiring).
        using (var scope = factory.Services.CreateScope())
        {
            var detected = await scope.ServiceProvider.GetRequiredService<RecurrenceClusterService>().ScanAsync(CancellationToken.None);
            Assert.Equal(0, detected);
        }

        var analyst = await AnalystAsync(await LoginAsync("admin"));
        var clusters = await DataAsync(await analyst.GetAsync("/api/v1/analytics/recurrence-clusters"));
        Assert.Equal(JsonValueKind.Array, clusters.GetProperty("items").ValueKind); // cursor page envelope
    }

    [Fact]
    public async Task Opening_a_dashboard_assembles_its_widgets_with_data()
    {
        var analyst = await AnalystAsync(await LoginAsync("admin"));
        var detail = await DataAsync(await analyst.GetAsync("/api/v1/dashboards/audit_committee"));

        Assert.Equal("audit_committee", detail.GetProperty("slug").GetString());
        var widgets = detail.GetProperty("widgets");
        Assert.True(widgets.GetArrayLength() >= 1);
        Assert.Contains(widgets.EnumerateArray(), w => w.GetProperty("metricKey").GetString() == "material_findings");
    }

    [Fact]
    public async Task Risk_metrics_are_captured_into_the_snapshot_and_read_back_as_a_trend()
    {
        var admin = await LoginAsync("admin");    // Administrator → ManageRisk
        var analyst = await AnalystAsync(admin);  // Audit Manager → ViewAnalytics

        // Seed a distinctive open risk (inherent 4×4 = 16, Critical) so the register is non-empty.
        var users = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        var ownerId = users.GetProperty("items").EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "manager@auditx.local").GetProperty("id").GetGuid();
        await DataAsync(await admin.PostAsJsonAsync("/api/v1/risks", new
        {
            title = $"Trend probe {Guid.NewGuid():N}", category = "Strategic",
            ownerUserId = ownerId, inherentLikelihood = 4, inherentImpact = 4,
        }));

        // Capture today's KPI snapshot (the daily job's work) through the real service against SQL.
        int captured;
        using (var scope = factory.Services.CreateScope())
        {
            captured = await scope.ServiceProvider
                .GetRequiredService<AnalyticsSnapshotCaptureService>().CaptureAsync();
        }
        Assert.True(captured > 0);

        // The open-risk count is now a readable trend metric (today's bucket carries the current value).
        var openTrend = await DataAsync(await analyst.GetAsync(
            "/api/v1/analytics/metric-comparison?metric=risk.open_total&period=month"));
        Assert.Equal("risk.open_total", openTrend.GetProperty("metricKey").GetString());
        Assert.True(openTrend.GetProperty("current").GetDecimal() >= 1);

        // The mean current-score metric is captured too (a risk is open, so the row is not omitted).
        var scoreTrend = await DataAsync(await analyst.GetAsync(
            "/api/v1/analytics/metric-comparison?metric=risk.avg_current_score&period=month"));
        Assert.True(scoreTrend.GetProperty("current").GetDecimal() >= 1);
    }
}
