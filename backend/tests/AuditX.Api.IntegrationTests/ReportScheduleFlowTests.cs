using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Domain.Enums;
using AuditX.Domain.Scheduling;
using AuditX.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// D3-C recurring report schedules: CRUD + permission gating, and the runner background job that generates and
/// delivers a due schedule's standalone report.
/// </summary>
public sealed class ReportScheduleFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grants the Audit Manager role (holds ScheduleReports AND ViewReport) to the manager dev user, logged in.</summary>
    private async Task<HttpClient> ManagerAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    [Fact]
    public async Task Create_list_update_and_delete_a_schedule()
    {
        var admin = await LoginAsync("admin"); // Administrator holds ScheduleReports

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/report-schedules", new
        {
            name = "Weekly executive summary",
            kind = "executive_summary",
            cadence = "weekly",
            recipientEmails = new[] { "board@bank.local" },
        }));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("executive_summary", created.GetProperty("kind").GetString());
        Assert.Equal("weekly", created.GetProperty("cadence").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
        // First run is one cadence out — never immediate (no surprise send on save).
        Assert.True(created.GetProperty("nextRunAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddDays(6));

        var list = await DataAsync(await admin.GetAsync("/api/v1/report-schedules"));
        Assert.Contains(list.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

        var updated = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/report-schedules/{id}", new
        {
            name = "Quarterly executive summary",
            cadence = "quarterly",
            recipientEmails = new[] { "board@bank.local", "chair@bank.local" },
            isActive = false,
            version = Version(created),
        }));
        Assert.Equal("quarterly", updated.GetProperty("cadence").GetString());
        Assert.False(updated.GetProperty("isActive").GetBoolean());

        var delete = await admin.DeleteAsync($"/api/v1/report-schedules/{id}?version={Uri.EscapeDataString(Version(updated))}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await DataAsync(await admin.GetAsync("/api/v1/report-schedules"));
        Assert.DoesNotContain(afterDelete.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Engagement_kind_is_rejected()
    {
        var admin = await LoginAsync("admin");
        var resp = await admin.PostAsJsonAsync("/api/v1/report-schedules", new
        {
            name = "bad", kind = "audit_engagement", cadence = "daily", recipientEmails = new[] { "x@y.z" },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Scheduling_scorecards_requires_the_performance_analytics_permission()
    {
        var admin = await LoginAsync("admin"); // holds ScheduleReports but NOT PerformanceAnalyticsView
        var forbidden = await admin.PostAsJsonAsync("/api/v1/report-schedules", new
        {
            name = "Board scorecards", kind = "performance_scorecards", cadence = "monthly", recipientEmails = new[] { "board@bank.local" },
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // The Audit Manager holds PerformanceAnalyticsView, so may schedule the sensitive scorecards kind.
        var manager = await ManagerAsync(admin);
        var ok = await manager.PostAsJsonAsync("/api/v1/report-schedules", new
        {
            name = "Board scorecards", kind = "performance_scorecards", cadence = "monthly", recipientEmails = new[] { "board@bank.local" },
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Managing_schedules_requires_the_schedule_permission()
    {
        var auditee = await LoginAsync("auditee"); // no ScheduleReports
        var resp = await auditee.PostAsJsonAsync("/api/v1/report-schedules", new
        {
            name = "nope", kind = "executive_summary", cadence = "weekly", recipientEmails = new[] { "x@y.z" },
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task The_runner_generates_and_delivers_a_due_schedule_then_advances_it()
    {
        var admin = await LoginAsync("admin");
        var manager = await ManagerAsync(admin); // Audit Manager: holds ScheduleReports AND ViewReport

        // Seed a schedule already due (first run in the past) — the domain factory allows it; the create COMMAND
        // forces one-cadence-out, so we seed directly to exercise the runner.
        Guid scheduleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recipientsJson = new AuditX.Application.Scheduling.ReportScheduleRecipients([], ["board@bank.local"]).ToJson();
            var schedule = ReportSchedule.Create(
                "Due executive summary", ReportKind.ExecutiveSummary, ReportCadence.Daily,
                recipientsJson, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-5));
            db.ReportSchedules.Add(schedule);
            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AuditX.Api.BackgroundJobs.ReportScheduleRunnerJob>()
                .RunAsync(CancellationToken.None);
        }

        // The schedule advanced (next run is now in the future) and points at the report it produced.
        var reloaded = await DataAsync(await manager.GetAsync($"/api/v1/report-schedules/{scheduleId}"));
        Assert.True(reloaded.GetProperty("nextRunAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        Assert.NotEqual(JsonValueKind.Null, reloaded.GetProperty("lastReportId").ValueKind);

        // The produced report completed and was delivered to the recipient.
        var reportId = reloaded.GetProperty("lastReportId").GetGuid();
        var report = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}"));
        Assert.Equal("completed", report.GetProperty("status").GetString());

        var distributions = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}/distributions"));
        Assert.NotEmpty(distributions.GetProperty("items").EnumerateArray());
    }
}
