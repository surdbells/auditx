using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class ConfigurationFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Domain = "exception_defaults";

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

    private static string Def(int critical = 14, int high = 30, int medium = 45, int low = 60, int window = 24, int threshold = 3) =>
        $"{{\"target_days\":{{\"critical\":{critical},\"high\":{high},\"medium\":{medium},\"low\":{low}}},\"recurrence_window_months\":{window},\"recurrence_threshold\":{threshold}}}";

    private static int HighTargetDays(JsonElement versionDto)
    {
        using var def = JsonDocument.Parse(versionDto.GetProperty("definitionJson").GetString()!);
        return def.RootElement.GetProperty("target_days").GetProperty("high").GetInt32();
    }

    [Fact]
    public async Task Seeded_default_reproduces_the_pre_m12_exception_defaults()
    {
        var admin = await LoginAsync("admin");
        var active = await DataAsync(await admin.GetAsync($"/api/v1/configurations/{Domain}"));

        Assert.True(active.GetProperty("isActive").GetBoolean());
        Assert.Equal(30, HighTargetDays(active)); // High remediation target = 30 days, as before M12
    }

    [Fact]
    public async Task Reads_require_the_view_config_permission()
    {
        var auditee = await LoginAsync("auditee"); // roleless → no ViewConfig
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync($"/api/v1/configurations/{Domain}")).StatusCode);
    }

    [Fact]
    public async Task Creating_a_draft_with_an_invalid_definition_is_rejected()
    {
        var admin = await LoginAsync("admin");
        var bad = await admin.PostAsJsonAsync($"/api/v1/configurations/{Domain}",
            new { definitionJson = Def(critical: -5), changeReason = "Trying to set a negative target window" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }

    [Fact]
    public async Task Rolling_back_to_the_active_version_is_rejected()
    {
        var admin = await LoginAsync("admin");
        var active = await DataAsync(await admin.GetAsync($"/api/v1/configurations/{Domain}"));
        var activeVersion = active.GetProperty("versionNumber").GetInt32();

        var rollback = await admin.PostAsJsonAsync($"/api/v1/configurations/{Domain}/rollback",
            new { toVersionNumber = activeVersion, changeReason = "Attempting to roll back to the active version" });
        Assert.Equal(HttpStatusCode.Conflict, rollback.StatusCode);
    }

    [Fact]
    public async Task Create_then_activate_routes_through_maker_checker_and_switches_the_active_version()
    {
        var admin = await LoginAsync("admin");

        // Create a new draft tightening the High target to 20 days.
        var created = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/configurations/{Domain}",
            new { definitionJson = Def(high: 20), changeReason = "Tightening the High remediation SLA to 20 days" }));
        var newVersion = created.GetProperty("versionNumber").GetInt32();
        Assert.False(created.GetProperty("isActive").GetBoolean());

        // Activation is maker-checker-gated → 202 + pendingActionId, not yet active.
        var activate = await admin.PostAsJsonAsync($"/api/v1/configurations/{Domain}/versions/{newVersion}/activate",
            new { changeReason = "Adopting the tightened High remediation SLA" });
        Assert.Equal(HttpStatusCode.Accepted, activate.StatusCode);
        var pendingId = (await DataAsync(activate)).GetProperty("pendingActionId").GetGuid();

        // Still the old active version until approved.
        var stillOld = await DataAsync(await admin.GetAsync($"/api/v1/configurations/{Domain}"));
        Assert.NotEqual(newVersion, stillOld.GetProperty("versionNumber").GetInt32());

        // The maker cannot approve their own activation.
        var selfApprove = await admin.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);

        // A second user approves → the switch applies atomically.
        var manager = await LoginAsync("manager");
        var approve = await manager.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        // The new version is now active (and the active-config cache reflects it post-commit).
        var nowActive = await DataAsync(await admin.GetAsync($"/api/v1/configurations/{Domain}"));
        Assert.Equal(newVersion, nowActive.GetProperty("versionNumber").GetInt32());
        Assert.Equal(20, HighTargetDays(nowActive));

        // Exactly one active version remains for the domain.
        var versions = await DataAsync(await admin.GetAsync($"/api/v1/configurations/{Domain}/versions?limit=100"));
        var activeCount = versions.GetProperty("items").EnumerateArray().Count(v => v.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, activeCount);
    }
}
