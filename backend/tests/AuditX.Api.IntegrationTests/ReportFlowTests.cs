using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Reports.Generation;
using AuditX.Application.Reports.Retention;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

public sealed class ReportFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>Build an audit and drive it to UnderReview (last item finalised) so it is reportable.</summary>
    private async Task<Guid> BuildReportableAuditAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Report audit {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Only question", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        // Finalising the only required item auto-transitions the audit to UnderReview (M4).
        return auditId;
    }

    /// <summary>
    /// Administrator holds ConfigureReports but NOT GenerateReport/ViewReport (segregated to Audit Manager).
    /// Grant the Audit Manager role to the seeded 'manager' user (who is also set as each audit's lead, so the
    /// in-handler ReportAccess scope passes) and return a logged-in client for them. Best-effort grant so it is
    /// idempotent across the class's shared database.
    /// </summary>
    private async Task<HttpClient> ReporterAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    private async Task<JsonElement> PollUntilSettledAsync(HttpClient client, Guid reportId)
    {
        for (var i = 0; i < 20; i++)
        {
            var report = await DataAsync(await client.GetAsync($"/api/v1/reports/{reportId}"));
            var status = report.GetProperty("status").GetString();
            if (status is "completed" or "failed")
            {
                return report;
            }

            await Task.Delay(250);
        }

        throw new Xunit.Sdk.XunitException("Report did not settle in time.");
    }

    /// <summary>
    /// Best-effort nudge: run the generation service directly so the test does not wait on the Hangfire schedule.
    /// The enqueued Hangfire job runs the same idempotent service, so if the two race for the same report one loses
    /// on the rowversion — swallow that here and let <see cref="PollUntilSettledAsync"/> be the source of truth.
    /// A clean run also proves the renderer DI graph resolves (a circular registration would throw on resolve).
    /// </summary>
    private async Task NudgeGenerationAsync(Guid reportId)
    {
        try
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportGenerationService>().RunAsync(reportId, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Hangfire job won the race (or it is mid-flight) — polling will observe the settled report.
        }
    }

    [Fact]
    public async Task Generate_runs_async_then_the_report_is_downloadable_and_hash_verifies()
    {
        var admin = await LoginAsync("admin");
        var auditId = await BuildReportableAuditAsync(admin);
        var manager = await ReporterAsync(admin);

        var accepted = await manager.PostAsJsonAsync($"/api/v1/audits/{auditId}/reports", new { docx = false });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();

        await NudgeGenerationAsync(reportId);

        var report = await PollUntilSettledAsync(manager, reportId);
        Assert.Equal("completed", report.GetProperty("status").GetString());
        Assert.Contains(report.GetProperty("producedArtefacts").EnumerateArray(), a => a.GetProperty("format").GetString() == "html");

        var download = await manager.GetAsync($"/api/v1/reports/{reportId}/download?format=html");
        download.EnsureSuccessStatusCode();
        Assert.True((await download.Content.ReadAsByteArrayAsync()).Length > 0);

        var verify = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}/verify-hash"));
        Assert.True(verify.GetProperty("match").GetBoolean());

        // D2: the download is written to the append-only audit trail — visible on the report's object history.
        var trail = await DataAsync(await admin.GetAsync($"/api/v1/audit-trail/object/report/{reportId}"));
        Assert.Contains(trail.GetProperty("items").EnumerateArray(),
            e => e.GetProperty("eventType").GetString() == "report_downloaded");
    }

    [Fact]
    public async Task Generating_again_produces_a_distinct_newer_version()
    {
        var admin = await LoginAsync("admin");
        var auditId = await BuildReportableAuditAsync(admin);
        var manager = await ReporterAsync(admin);

        async Task<int> GenerateAsync()
        {
            var accepted = await manager.PostAsJsonAsync($"/api/v1/audits/{auditId}/reports", new { docx = false });
            var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();
            await NudgeGenerationAsync(reportId);
            return (await PollUntilSettledAsync(manager, reportId)).GetProperty("versionNumber").GetInt32();
        }

        var v1 = await GenerateAsync();
        var v2 = await GenerateAsync();
        Assert.True(v2 > v1);

        var versions = await DataAsync(await manager.GetAsync($"/api/v1/audits/{auditId}/reports"));
        Assert.True(versions.GetProperty("items").GetArrayLength() >= 2); // per-audit reports are now offset-paged
    }

    [Fact]
    public async Task Standalone_report_generates_downloads_verifies_and_lists()
    {
        var admin = await LoginAsync("admin");
        var manager = await ReporterAsync(admin); // Audit Manager holds GenerateReport + ViewAnalytics

        var accepted = await manager.PostAsJsonAsync("/api/v1/reports/standalone", new { kind = "executive_summary", docx = false });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();

        await NudgeGenerationAsync(reportId);

        var report = await PollUntilSettledAsync(manager, reportId);
        Assert.Equal("completed", report.GetProperty("status").GetString());
        Assert.Equal("executive_summary", report.GetProperty("kind").GetString());
        // A standalone report has no audit.
        Assert.True(report.GetProperty("auditId").ValueKind == JsonValueKind.Null);

        // HTML (canonical) + PDF + CSV + XLSX are always produced for export.
        var formats = report.GetProperty("producedArtefacts").EnumerateArray()
            .Select(a => a.GetProperty("format").GetString()).ToArray();
        Assert.Contains("html", formats);
        Assert.Contains("pdf", formats);
        Assert.Contains("csv", formats);
        Assert.Contains("xlsx", formats);

        foreach (var fmt in new[] { "html", "pdf", "csv", "xlsx" })
        {
            var dl = await manager.GetAsync($"/api/v1/reports/{reportId}/download?format={fmt}");
            dl.EnsureSuccessStatusCode();
            Assert.True((await dl.Content.ReadAsByteArrayAsync()).Length > 0);
        }

        // The PDF is a real, server-generated PDF (magic bytes %PDF-).
        var pdf = await (await manager.GetAsync($"/api/v1/reports/{reportId}/download?format=pdf")).Content.ReadAsByteArrayAsync();
        Assert.True(pdf.Length > 4 && pdf[0] == (byte)'%' && pdf[1] == (byte)'P' && pdf[2] == (byte)'D' && pdf[3] == (byte)'F');

        var download = await manager.GetAsync($"/api/v1/reports/{reportId}/download?format=html");
        download.EnsureSuccessStatusCode();
        Assert.True((await download.Content.ReadAsByteArrayAsync()).Length > 0);

        var verify = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}/verify-hash"));
        Assert.True(verify.GetProperty("match").GetBoolean());

        var listed = await DataAsync(await manager.GetAsync("/api/v1/reports/standalone?kind=executive_summary"));
        Assert.Contains(listed.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == reportId);
    }

    [Theory]
    [InlineData("annual_plan_status")]
    [InlineData("kpi_pack")]
    [InlineData("audit_coverage")]
    [InlineData("findings_register")]
    [InlineData("sanctions_consistency")]
    [InlineData("performance_scorecards")]
    public async Task Standalone_report_of_each_kind_generates_and_verifies(string kind)
    {
        var admin = await LoginAsync("admin");
        var manager = await ReporterAsync(admin); // Audit Manager holds GenerateReport + ViewAnalytics + PerformanceAnalyticsView

        var accepted = await manager.PostAsJsonAsync("/api/v1/reports/standalone", new { kind, docx = false });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();

        await NudgeGenerationAsync(reportId);

        var report = await PollUntilSettledAsync(manager, reportId);
        Assert.Equal("completed", report.GetProperty("status").GetString());
        Assert.Equal(kind, report.GetProperty("kind").GetString());

        var verify = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}/verify-hash"));
        Assert.True(verify.GetProperty("match").GetBoolean());
    }

    [Fact]
    public async Task Standalone_reports_require_the_analytics_permission()
    {
        var auditee = await LoginAsync("auditee"); // no analytics permission
        var response = await auditee.GetAsync("/api/v1/reports/standalone");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Report_templates_have_a_single_active_version_after_activation()
    {
        var admin = await LoginAsync("admin");

        var seeded = await DataAsync(await admin.GetAsync("/api/v1/report-templates"));
        Assert.Contains(seeded.EnumerateArray(), t => t.GetProperty("isActive").GetBoolean());

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/report-templates",
            new { name = $"Tmpl {Guid.NewGuid():N}", templateDefinition = "{\"sections\":[]}" }));
        var newId = created.GetProperty("id").GetGuid();

        await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/report-templates/{newId}/activate",
            new { reason = "Adopting the new report template for FY27 reporting" }));

        var after = await DataAsync(await admin.GetAsync("/api/v1/report-templates"));
        Assert.Equal(1, after.EnumerateArray().Count(t => t.GetProperty("isActive").GetBoolean()));
        Assert.True(after.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == newId).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Distribution_outcome_confirms_once_delivered_then_locks()
    {
        var admin = await LoginAsync("admin");
        var auditId = await BuildReportableAuditAsync(admin);
        var manager = await ReporterAsync(admin); // Audit Manager → DistributeReport + report scope (lead)

        // Generate + distribute to a directory user; the row is recorded Pending (accepted by the relay).
        var accepted = await manager.PostAsJsonAsync($"/api/v1/audits/{auditId}/reports", new { docx = false });
        var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();
        await NudgeGenerationAsync(reportId);
        await PollUntilSettledAsync(manager, reportId);

        var users = await UsersByEmailAsync(admin);
        (await manager.PostAsJsonAsync($"/api/v1/reports/{reportId}/distribute", new
        {
            recipientUserIds = new[] { users["auditee@auditx.local"] },
            recipientEmailAddresses = Array.Empty<string>(),
        })).EnsureSuccessStatusCode();

        var log = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}/distributions"));
        var row = log.GetProperty("items").EnumerateArray().First();
        var distributionId = row.GetProperty("id").GetGuid();
        Assert.Equal("pending", row.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("outcomeRecordedAt").ValueKind);

        // Confirm delivered → outcome flips with the confirmation evidence stamped.
        var confirmed = await DataAsync(await manager.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/distributions/{distributionId}/outcome", new { outcome = "delivered" }));
        Assert.Equal("delivered", confirmed.GetProperty("outcome").GetString());
        Assert.NotEqual(JsonValueKind.Null, confirmed.GetProperty("outcomeRecordedAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, confirmed.GetProperty("outcomeRecordedBy").ValueKind);

        // A confirmed outcome is final — a second confirmation conflicts.
        var again = await manager.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/distributions/{distributionId}/outcome", new { outcome = "bounced" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // Confirming requires the DistributeReport permission.
        var auditee = await LoginAsync("auditee");
        var denied = await auditee.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/distributions/{distributionId}/outcome", new { outcome = "delivered" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // An unknown outcome value is rejected as a business-rule violation.
        var bad = await manager.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/distributions/{distributionId}/outcome", new { outcome = "read" });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);
    }

    [Fact]
    public async Task Retention_policy_stamps_reports_then_the_job_expires_them_and_blocks_download()
    {
        var admin = await LoginAsync("admin");

        // Configure a 12-month retention policy via the admin bank-settings PATCH (full replace of current values).
        var s = await DataAsync(await admin.GetAsync("/api/v1/admin/bank-settings"));
        (await admin.PatchAsJsonAsync("/api/v1/admin/bank-settings", new
        {
            bankDisplayName = s.GetProperty("bankDisplayName").GetString(),
            timezone = s.GetProperty("timezone").GetString(),
            localeDefault = s.GetProperty("localeDefault").GetString(),
            adProvisioningFilterOuDn = (string?)null,
            adProvisioningFilterGroupSid = (string?)null,
            allowOverlappingPlanPeriods = false,
            allowAuditLaunchBeforeApproval = false,
            primaryColor = s.GetProperty("primaryColor").GetString(),
            accentColor = s.GetProperty("accentColor").GetString(),
            logoDataUri = (string?)null,
            iconDataUri = (string?)null,
            showOverview = true,
            showWalkthrough = true,
            reportRetentionMonths = 12,
        })).EnsureSuccessStatusCode();

        // Generate a standalone report — completion stamps RetentionUntil from the policy.
        var manager = await ReporterAsync(admin);
        var accepted = await manager.PostAsJsonAsync("/api/v1/reports/standalone", new { kind = "executive_summary", docx = false });
        var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();
        await NudgeGenerationAsync(reportId);
        await PollUntilSettledAsync(manager, reportId);

        // The policy stamped a retention date; backdate it into the past, then run the retention-expiry service.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stamped = await db.Reports.AsNoTracking().FirstAsync(r => r.Id == reportId);
            Assert.NotNull(stamped.RetentionUntil);

            await db.Reports.Where(r => r.Id == reportId)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.RetentionUntil, DateTimeOffset.UtcNow.AddDays(-1)));

            var expired = await scope.ServiceProvider.GetRequiredService<ReportRetentionService>().ExpireDueAsync(CancellationToken.None);
            Assert.True(expired >= 1);
        }

        // The report is now Expired and its artefacts are no longer served.
        var after = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}"));
        Assert.Equal("expired", after.GetProperty("status").GetString());

        var blocked = await manager.GetAsync($"/api/v1/reports/{reportId}/download?format=html");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task Reports_require_the_view_permission()
    {
        var auditee = await LoginAsync("auditee"); // dev 'auditee' has no role → no ViewReport
        var response = await auditee.GetAsync($"/api/v1/audits/{Guid.NewGuid()}/reports");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
