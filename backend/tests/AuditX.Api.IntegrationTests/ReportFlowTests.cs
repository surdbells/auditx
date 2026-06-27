using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Reports.Generation;
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
        Assert.True(versions.GetArrayLength() >= 2);
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
    public async Task Reports_require_the_view_permission()
    {
        var auditee = await LoginAsync("auditee"); // dev 'auditee' has no role → no ViewReport
        var response = await auditee.GetAsync($"/api/v1/audits/{Guid.NewGuid()}/reports");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
