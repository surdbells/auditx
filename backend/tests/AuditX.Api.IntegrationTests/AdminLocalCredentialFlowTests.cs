using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Admin management of local credentials (M1 local auth): creating a local user with each initial-password
/// method, duplicate-username rejection, and unlocking a locked account. Requires Docker.
/// </summary>
public sealed class AdminLocalCredentialFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SetPass = "L0cal&Password!";

    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

    private async Task<HttpClient> AdminAsync()
    {
        var client = NewClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private async Task SetPolicyAsync(HttpClient admin, int maxFailedAttempts = 5, int lockoutMinutes = 15)
    {
        var body = new
        {
            enableLocalPasswords = true,
            minLength = 12,
            requireUppercase = true,
            requireLowercase = true,
            requireDigit = true,
            requireSymbol = true,
            historyDepth = 5,
            expiryDays = 0,
            maxFailedAttempts,
            lockoutMinutes,
        };
        (await admin.PatchAsJsonAsync("/api/v1/admin/institution-settings/password-policy", body)).EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password)
        => client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });

    private static object CreateBody(string username, string method, string? password) => new
    {
        email = $"{username}@example.com",
        firstName = "Local",
        lastName = "User",
        username,
        roleNames = Array.Empty<string>(),
        method,
        password,
    };

    [Fact]
    public async Task Admin_creates_a_local_user_with_a_set_password_that_forces_a_change()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/users/local", CreateBody("setpass_user", "set_password", SetPass)));
        Assert.Equal(JsonValueKind.Null, created.GetProperty("generatedPassword").ValueKind);

        var session = await DataAsync(await LoginAsync(NewClient(), "setpass_user", SetPass));
        Assert.True(session.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Admin_generate_temp_returns_a_usable_temporary_password()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/users/local", CreateBody("temp_user", "generate_temp", null)));
        var temp = created.GetProperty("generatedPassword").GetString();
        Assert.False(string.IsNullOrWhiteSpace(temp));

        var session = await DataAsync(await LoginAsync(NewClient(), "temp_user", temp!));
        Assert.True(session.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Invite_method_sets_no_password()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/users/local", CreateBody("invite_user", "invite", null)));
        Assert.Equal(JsonValueKind.Null, created.GetProperty("generatedPassword").ValueKind);

        // No credential yet → cannot sign in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "invite_user", SetPass)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_username_is_rejected()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin);

        (await admin.PostAsJsonAsync("/api/v1/users/local", CreateBody("dupe_user", "set_password", SetPass))).EnsureSuccessStatusCode();
        var second = await admin.PostAsJsonAsync("/api/v1/users/local", CreateBody("dupe_user", "set_password", SetPass));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Admin_can_unlock_a_locked_account()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, maxFailedAttempts: 2, lockoutMinutes: 15);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/users/local", CreateBody("unlock_user", "set_password", SetPass)));
        var userId = created.GetProperty("userId").GetGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "unlock_user", "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "unlock_user", "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "unlock_user", SetPass)).StatusCode); // locked

        (await admin.PostAsync($"/api/v1/users/{userId}/unlock", content: null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), "unlock_user", SetPass)).StatusCode);
    }
}
