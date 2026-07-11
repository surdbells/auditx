using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Bank settings: the page-guide visibility toggles (Overview / Walkthrough) round-trip through the admin
/// settings surface and are reflected on the anonymous /branding surface the SPA page-guide reads.
/// </summary>
public sealed class BankSettingsFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    [Fact]
    public async Task Page_guide_visibility_toggles_round_trip_and_surface_on_branding()
    {
        var admin = await LoginAsync("admin");

        // Both buttons + the auto-start tour default to on.
        var current = await DataAsync(await admin.GetAsync("/api/v1/admin/bank-settings"));
        Assert.True(current.GetProperty("showOverview").GetBoolean());
        Assert.True(current.GetProperty("showWalkthrough").GetBoolean());
        Assert.True(current.GetProperty("autoStartWalkthrough").GetBoolean());

        // Idle-logout policy defaults: warn after 15 idle minutes, 60s countdown.
        Assert.Equal(15, current.GetProperty("idleTimeoutMinutes").GetInt32());
        Assert.Equal(60, current.GetProperty("idleWarningSeconds").GetInt32());

        // Hide Overview, keep Walkthrough, disable the auto-start tour (full-replace PATCH).
        var updated = await DataAsync(await admin.PatchAsJsonAsync("/api/v1/admin/bank-settings", new
        {
            bankDisplayName = current.GetProperty("bankDisplayName").GetString(),
            timezone = current.GetProperty("timezone").GetString(),
            localeDefault = current.GetProperty("localeDefault").GetString(),
            adProvisioningFilterOuDn = (string?)null,
            adProvisioningFilterGroupSid = (string?)null,
            allowOverlappingPlanPeriods = current.GetProperty("allowOverlappingPlanPeriods").GetBoolean(),
            allowAuditLaunchBeforeApproval = current.GetProperty("allowAuditLaunchBeforeApproval").GetBoolean(),
            primaryColor = current.GetProperty("primaryColor").GetString(),
            accentColor = current.GetProperty("accentColor").GetString(),
            logoDataUri = (string?)null,
            iconDataUri = (string?)null,
            showOverview = false,
            showWalkthrough = true,
            autoStartWalkthrough = false,
            idleTimeoutMinutes = 30,
            idleWarningSeconds = 90,
        }));
        Assert.False(updated.GetProperty("showOverview").GetBoolean());
        Assert.True(updated.GetProperty("showWalkthrough").GetBoolean());
        Assert.False(updated.GetProperty("autoStartWalkthrough").GetBoolean());
        Assert.Equal(30, updated.GetProperty("idleTimeoutMinutes").GetInt32());
        Assert.Equal(90, updated.GetProperty("idleWarningSeconds").GetInt32());

        // Persisted on re-read.
        var reread = await DataAsync(await admin.GetAsync("/api/v1/admin/bank-settings"));
        Assert.False(reread.GetProperty("showOverview").GetBoolean());
        Assert.True(reread.GetProperty("showWalkthrough").GetBoolean());
        Assert.False(reread.GetProperty("autoStartWalkthrough").GetBoolean());
        Assert.Equal(30, reread.GetProperty("idleTimeoutMinutes").GetInt32());
        Assert.Equal(90, reread.GetProperty("idleWarningSeconds").GetInt32());

        // The anonymous branding surface (which the SPA page-guide reads app-wide) reflects the toggles.
        var anon = NewClient();
        var branding = await DataAsync(await anon.GetAsync("/api/v1/branding"));
        Assert.False(branding.GetProperty("showOverview").GetBoolean());
        Assert.True(branding.GetProperty("showWalkthrough").GetBoolean());
        Assert.False(branding.GetProperty("autoStartWalkthrough").GetBoolean());
        Assert.Equal(30, branding.GetProperty("idleTimeoutMinutes").GetInt32());
        Assert.Equal(90, branding.GetProperty("idleWarningSeconds").GetInt32());
    }

    [Fact]
    public async Task Reading_bank_settings_requires_the_view_permission()
    {
        var auditee = await LoginAsync("auditee"); // no ViewBankSettings
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync("/api/v1/admin/bank-settings")).StatusCode);
    }
}
