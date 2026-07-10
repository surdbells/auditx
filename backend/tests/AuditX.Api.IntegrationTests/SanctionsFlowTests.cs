using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Sanctions;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

public sealed class SanctionsFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    [Fact]
    public async Task Active_sanctions_grid_is_seeded_and_readable()
    {
        var admin = await LoginAsync("admin");
        var grid = await DataAsync(await admin.GetAsync("/api/v1/sanctions/grid"));
        Assert.True(grid.GetProperty("isActive").GetBoolean());
        Assert.Contains("cells", grid.GetProperty("gridDefinitionJson").GetString());
    }

    [Fact]
    public async Task Cases_list_returns_a_page_with_subjects_masked()
    {
        // Administrator does NOT hold ViewSanctions (deliberately segregated) — grant the Audit Manager role to
        // the seeded 'manager' user, then query as that authorized persona.
        var admin = await LoginAsync("admin");
        var users = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        var managerId = users.GetProperty("items").EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "manager@auditx.local").GetProperty("id").GetGuid();
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var auditManagerRoleId = roles.EnumerateArray()
            .First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/users/{managerId}/roles", new { roleId = auditManagerRoleId, scopeValue = (string?)null }))
            .EnsureSuccessStatusCode();

        var manager = await LoginAsync("manager");
        var page = await DataAsync(await manager.GetAsync("/api/v1/sanctions/cases?pageSize=50"));
        Assert.True(page.TryGetProperty("items", out var items));
        Assert.All(items.EnumerateArray(), c => Assert.True(c.GetProperty("subjectMasked").GetBoolean()));
    }

    [Fact]
    public async Task Sanctions_require_the_view_permission()
    {
        var auditee = await LoginAsync("auditee"); // Auditee role does not hold ViewSanctions
        var response = await auditee.GetAsync("/api/v1/sanctions/cases?pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Activating_a_new_grid_version_switches_atomically_without_violating_the_unique_index()
    {
        // Exercises the active-grid switch directly against real SQL + the filtered unique index on is_active=1.
        // The deactivate must be ordered before the activate, else the index sees two active rows and throws.
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var grids = sp.GetRequiredService<ISanctionsGridRepository>();
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var clock = sp.GetRequiredService<IClock>();

        var nextVersion = await grids.GetMaxVersionNumberAsync() + 1;
        var draft = SanctionsGridVersion.CreateDraft(nextVersion, "{\"cells\":{}}", Guid.NewGuid(), clock.UtcNow);
        grids.Add(draft);
        await uow.SaveChangesAsync();

        await grids.DeactivateActiveAsync();
        draft.Activate("Integration test activates a fresh sanctions grid version", Guid.NewGuid(), clock.UtcNow);
        await uow.SaveChangesAsync(); // must not throw a unique-index violation

        var active = await grids.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(draft.Id, active!.Id);
    }
}
