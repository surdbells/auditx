using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Identity.Passwords;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using AuditX.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Local-password authentication (M1 local auth): login routing, lockout, must-change, self-service change,
/// and the forgot/reset flow. Local users + credentials are seeded directly (the admin creation path lands
/// in a later phase). Requires Docker.
/// </summary>
public sealed class LocalPasswordFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "L0cal&Password!";
    private const string NewPassword = "N3w&Password!!";

    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

    private async Task<HttpClient> AdminAsync()
    {
        var client = NewClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private async Task SetPolicyAsync(HttpClient admin, bool enable, int maxFailedAttempts = 5, int lockoutMinutes = 15, int historyDepth = 5)
    {
        var body = new
        {
            enableLocalPasswords = enable,
            minLength = 12,
            requireUppercase = true,
            requireLowercase = true,
            requireDigit = true,
            requireSymbol = true,
            historyDepth,
            expiryDays = 0,
            maxFailedAttempts,
            lockoutMinutes,
        };
        (await admin.PatchAsJsonAsync("/api/v1/admin/institution-settings/password-policy", body)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> SeedLocalUserAsync(string username, string password, bool mustChange = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = User.CreateLocal(username, $"{username}@example.com", "Local", "User");
        var credential = UserCredential.Create(user.Id, hasher.Hash(password), mustChange, clock.UtcNow);
        db.Users.Add(user);
        db.UserCredentials.Add(credential);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password)
        => client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });

    [Fact]
    public async Task Local_user_can_sign_in_and_repeated_failures_lock_the_account()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, enable: true, maxFailedAttempts: 3, lockoutMinutes: 15);
        await SeedLocalUserAsync("lockme", Password);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), "lockme", Password)).StatusCode);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "lockme", "wrong-password")).StatusCode);
        }

        // Even the correct password is now rejected while the lockout window is open.
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "lockme", Password)).StatusCode);
    }

    [Fact]
    public async Task Login_disabled_when_the_master_toggle_is_off()
    {
        var admin = await AdminAsync();
        await SeedLocalUserAsync("toggleoff", Password);

        await SetPolicyAsync(admin, enable: false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "toggleoff", Password)).StatusCode);

        await SetPolicyAsync(admin, enable: true);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), "toggleoff", Password)).StatusCode);
    }

    [Fact]
    public async Task Must_change_flag_surfaces_on_the_session()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, enable: true);
        await SeedLocalUserAsync("mustchange", Password, mustChange: true);

        var session = await DataAsync(await LoginAsync(NewClient(), "mustchange", Password));
        Assert.True(session.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Change_password_replaces_the_credential()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, enable: true);
        await SeedLocalUserAsync("changer", Password);

        var client = NewClient();
        (await LoginAsync(client, "changer", Password)).EnsureSuccessStatusCode();

        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), "changer", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), "changer", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Change_password_rejects_a_wrong_current_password()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, enable: true);
        await SeedLocalUserAsync("wrongcurrent", Password);

        var client = NewClient();
        (await LoginAsync(client, "wrongcurrent", Password)).EnsureSuccessStatusCode();

        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "not-it", newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, change.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_never_reveals_whether_the_account_exists()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, enable: true);
        await SeedLocalUserAsync("forgetful", Password);

        Assert.Equal(HttpStatusCode.NoContent,
            (await NewClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { usernameOrEmail = "forgetful" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await NewClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { usernameOrEmail = "no-such-user" })).StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_a_valid_token_sets_a_new_password()
    {
        var admin = await AdminAsync();
        await SetPolicyAsync(admin, enable: true);
        var userId = await SeedLocalUserAsync("resetme", Password);

        // Seed a reset token directly (the raw token is otherwise only delivered by email).
        var raw = CredentialTokens.GenerateRawToken();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            db.PasswordResetTokens.Add(PasswordResetToken.Issue(userId, CredentialTokens.Hash(raw), CredentialTokenPurpose.Reset, clock.UtcNow.AddHours(1)));
            await db.SaveChangesAsync();
        }

        var reset = await NewClient().PostAsJsonAsync("/api/v1/auth/reset-password", new { token = raw, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), "resetme", NewPassword)).StatusCode);

        // The token is single-use.
        var reuse = await NewClient().PostAsJsonAsync("/api/v1/auth/reset-password", new { token = raw, newPassword = "An0ther&Pass!" });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }
}
