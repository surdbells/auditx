using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Ac.Generation;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

public sealed class AcFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private async Task<Guid> RoleIdAsync(HttpClient admin, string roleName)
    {
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        return roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == roleName).GetProperty("id").GetGuid();
    }

    private async Task<Guid> UserIdAsync(HttpClient admin, string email)
    {
        var users = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return users.GetProperty("items").EnumerateArray().First(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();
    }

    /// <summary>Grant a seeded role to a dev user (best-effort; idempotent across the class DB) and log them in.</summary>
    private async Task<HttpClient> AsRoleAsync(HttpClient admin, string username, string email, string roleName)
    {
        var roleId = await RoleIdAsync(admin, roleName);
        var userId = await UserIdAsync(admin, email);
        await admin.PostAsJsonAsync($"/api/v1/users/{userId}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync(username);
    }

    [Fact]
    public async Task The_three_ac_roles_are_seeded()
    {
        var admin = await LoginAsync("admin");
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var names = roles.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToArray();
        Assert.Contains("Audit Committee Member", names);
        Assert.Contains("Audit Committee Chair", names);
        Assert.Contains("Chief Internal Auditor", names);
    }

    [Fact]
    public async Task Generate_approve_distribute_with_role_gated_visibility_and_downloads()
    {
        var admin = await LoginAsync("admin");
        var cia = await AsRoleAsync(admin, "manager", "manager@auditx.local", "Chief Internal Auditor");
        var member = await AsRoleAsync(admin, "auditee", "auditee@auditx.local", "Audit Committee Member");

        // CIA generates a pack (GenerateACPack + CIA in-handler) → 202.
        var accepted = await cia.PostAsJsonAsync("/api/v1/ac-packs/generate",
            new { periodStart = "2026-10-01", periodEnd = "2026-12-31", acMeetingLabel = "Q4 2026", docx = true });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var packId = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("acPackId").GetGuid();

        // Run the generation service directly (the Hangfire job runs the same idempotent service).
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AcPackGenerationService>().RunAsync(packId, CancellationToken.None);
        }

        // While PendingReview an AC member cannot see the pack (existence not leaked → 404).
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/v1/ac-packs/{packId}")).StatusCode);

        // CIA approves with a supplementary narrative (re-seals the artefact incl. the narrative).
        var approved = await DataAsync(await cia.PostAsJsonAsync($"/api/v1/ac-packs/{packId}/approve",
            new { supplementaryText = "Chief Internal Auditor's executive summary for the committee meeting." }));
        Assert.Equal("approved", approved.GetProperty("status").GetString());

        // Now an AC member can read + download (a per-requester redacted re-render); CIA gets the canonical blob.
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/v1/ac-packs/{packId}")).StatusCode);
        var memberDownload = await member.GetAsync($"/api/v1/ac-packs/{packId}/download?format=html");
        Assert.Equal(HttpStatusCode.OK, memberDownload.StatusCode);
        Assert.True((await memberDownload.Content.ReadAsByteArrayAsync()).Length > 0);

        var ciaDownload = await cia.GetAsync($"/api/v1/ac-packs/{packId}/download?format=html");
        Assert.Equal(HttpStatusCode.OK, ciaDownload.StatusCode); // canonical blob passes verify-on-read

        // An AC member cannot generate (lacks GenerateACPack).
        var memberGenerate = await member.PostAsJsonAsync("/api/v1/ac-packs/generate",
            new { periodStart = "2026-10-01", periodEnd = "2026-12-31", acMeetingLabel = (string?)null, docx = false });
        Assert.Equal(HttpStatusCode.Forbidden, memberGenerate.StatusCode);

        // Distribute to the AC cohort, then re-distribute → idempotent no-op (0 new recipients).
        var dist = await DataAsync(await cia.PostAsJsonAsync($"/api/v1/ac-packs/{packId}/distribute", new { }));
        Assert.True(dist.GetProperty("recipientCount").GetInt32() >= 1);
        var redist = await DataAsync(await cia.PostAsJsonAsync($"/api/v1/ac-packs/{packId}/distribute", new { }));
        Assert.Equal(0, redist.GetProperty("recipientCount").GetInt32());
    }

    [Fact]
    public async Task Ac_action_item_can_be_created_by_a_member_and_listed()
    {
        var admin = await LoginAsync("admin");
        var member = await AsRoleAsync(admin, "auditee", "auditee@auditx.local", "Audit Committee Member");

        var created = await DataAsync(await member.PostAsJsonAsync("/api/v1/ac-action-items",
            new { title = "Tighten branch cash controls", description = "Per AC review", dueDate = "2027-03-31" }));
        var itemId = created.GetProperty("id").GetGuid();

        var list = await DataAsync(await member.GetAsync("/api/v1/ac-action-items"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == itemId);
    }
}
