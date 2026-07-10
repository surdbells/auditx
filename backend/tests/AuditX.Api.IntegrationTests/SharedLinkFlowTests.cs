using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Reports.Generation;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// D3-B shareable report links: create → resolve (permission-enforcing) → revoke, plus the security invariant that a
/// link is never an access grant (a user without report access cannot resolve it) and only the creator can revoke.
/// </summary>
public sealed class SharedLinkFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grants the Audit Manager role (GenerateReport + ViewReport + ViewAnalytics) to a seeded user.</summary>
    private async Task<HttpClient> AsManagerAsync(HttpClient admin, string username, string email)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users[email]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync(username);
    }

    private async Task NudgeGenerationAsync(Guid reportId)
    {
        try
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportGenerationService>().RunAsync(reportId, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Hangfire job won the race; polling observes the settled report.
        }
    }

    private async Task<Guid> CompletedStandaloneReportAsync(HttpClient manager)
    {
        var accepted = await manager.PostAsJsonAsync("/api/v1/reports/standalone", new { kind = "executive_summary", docx = false });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var reportId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("reportId").GetGuid();

        await NudgeGenerationAsync(reportId);
        for (var i = 0; i < 20; i++)
        {
            var report = await DataAsync(await manager.GetAsync($"/api/v1/reports/{reportId}"));
            if (report.GetProperty("status").GetString() is "completed" or "failed")
            {
                return reportId;
            }

            await Task.Delay(250);
        }

        throw new Xunit.Sdk.XunitException("Report did not settle in time.");
    }

    [Fact]
    public async Task Create_resolve_revoke_and_permission_enforcement()
    {
        var admin = await LoginAsync("admin");
        var manager = await AsManagerAsync(admin, "manager", "manager@auditx.local");
        var reportId = await CompletedStandaloneReportAsync(manager);

        // Create a shareable link (no expiry).
        var link = await DataAsync(await manager.PostAsJsonAsync($"/api/v1/reports/{reportId}/share", new { expiresInDays = (int?)null }));
        var slug = link.GetProperty("slug").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(slug));
        Assert.True(link.GetProperty("isActive").GetBoolean());
        Assert.Equal("report", link.GetProperty("targetType").GetString());

        // The creator resolves it → the target descriptor (no content).
        var resolved = await DataAsync(await manager.GetAsync($"/api/v1/shared-links/{slug}"));
        Assert.Equal("report", resolved.GetProperty("targetType").GetString());
        Assert.Equal(reportId, resolved.GetProperty("targetId").GetGuid());

        // A link is NOT an access grant: a user without report access cannot resolve it.
        var auditee = await LoginAsync("auditee"); // no ViewReport
        var forbidden = await auditee.GetAsync($"/api/v1/shared-links/{slug}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Only the creator can revoke: a different report-capable user is refused.
        var otherManager = await AsManagerAsync(admin, "auditor", "auditor@auditx.local");
        var wrongRevoke = await otherManager.DeleteAsync($"/api/v1/shared-links/{link.GetProperty("id").GetGuid()}?version={Uri.EscapeDataString(link.GetProperty("version").GetString()!)}");
        Assert.Equal(HttpStatusCode.Forbidden, wrongRevoke.StatusCode);

        // The creator revokes it.
        var revoke = await manager.DeleteAsync($"/api/v1/shared-links/{link.GetProperty("id").GetGuid()}?version={Uri.EscapeDataString(link.GetProperty("version").GetString()!)}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // A revoked link no longer resolves (404, indistinguishable from never-existed).
        var afterRevoke = await manager.GetAsync($"/api/v1/shared-links/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, afterRevoke.StatusCode);

        // A random slug also 404s.
        var missing = await manager.GetAsync("/api/v1/shared-links/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
