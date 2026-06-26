using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class IdentityFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private static async Task<JsonElement> LoginAsync(HttpClient client, string username, string password = "Passw0rd!")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task Invalid_credentials_return_401()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_to_protected_endpoint_returns_401()
    {
        var client = NewClient();
        var response = await client.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_login_yields_administrator_role_and_can_list_roles()
    {
        var client = NewClient();
        var session = await LoginAsync(client, "admin");

        var roles = session.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Contains("AuditX Administrator", roles);
        Assert.Equal("active", session.GetProperty("status").GetString());

        var list = await client.GetAsync("/api/v1/roles");
        list.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("data").GetArrayLength() >= 4);
    }

    [Fact]
    public async Task First_login_user_is_awaiting_role_and_is_forbidden_from_admin_endpoints()
    {
        var client = NewClient();
        var session = await LoginAsync(client, "auditor");

        Assert.Equal("awaiting_role_assignment", session.GetProperty("status").GetString());
        Assert.Empty(session.GetProperty("roles").EnumerateArray());

        var forbidden = await client.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Permission_catalogue_is_available_to_authenticated_users()
    {
        var client = NewClient();
        await LoginAsync(client, "admin");

        var response = await client.GetAsync("/api/v1/permissions/catalogue");
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("data").GetArrayLength() > 20);
    }

    [Fact]
    public async Task Gated_role_creation_returns_202_and_maker_cannot_self_approve()
    {
        var client = NewClient();
        await LoginAsync(client, "admin");

        var create = await client.PostAsJsonAsync("/api/v1/roles", new
        {
            name = $"Regional Lead {Guid.NewGuid():N}",
            description = "Regional audit lead",
            permissions = Array.Empty<object>(),
            parentRoleIds = Array.Empty<Guid>(),
        });

        // role_permission_change is gated by default seed → the action is held pending.
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var pending = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var pendingId = pending.RootElement.GetProperty("data").GetProperty("pendingActionId").GetGuid();

        // The maker may not approve their own action.
        var approve = await client.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_session()
    {
        var client = NewClient();
        await LoginAsync(client, "admin");

        var me = await client.GetAsync("/api/v1/users/me");
        me.EnsureSuccessStatusCode();

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
