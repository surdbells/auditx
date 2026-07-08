using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuditX.Application.Abstractions.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Generic Active-Directory-over-REST identity provider. Many banks expose their directory only through an internal
/// HTTP gateway rather than raw LDAP connection details; this provider talks to that gateway's documented contract
/// (see <c>docs/identity-ad-rest-contract.md</c>): a credential-validation endpoint plus account/status lookups, each
/// returning the directory user and their AD groups. AuditX never stores passwords — it forwards them once to the
/// gateway for validation and keeps only the returned directory attributes. It is selectable alongside LDAP by
/// setting <c>Identity:Provider = ActiveDirectoryApi</c>.
/// </summary>
public sealed class ActiveDirectoryApiIdentityProvider(
    HttpClient httpClient,
    IOptions<ActiveDirectoryApiOptions> options,
    ILogger<ActiveDirectoryApiIdentityProvider> logger)
    : IIdentityProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ActiveDirectoryApiOptions _options = options.Value;

    public async Task<DirectoryUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                BuildUrl(_options.AuthenticatePath), new AuthRequest(username, password), Json, cancellationToken);

            // A rejected credential is the expected "invalid login" signal, not an error to surface.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AD gateway authenticate returned {Status} for {User}.", (int)response.StatusCode, username);
                return null;
            }

            var user = await response.Content.ReadFromJsonAsync<AdRestUser>(Json, cancellationToken);
            return user?.ToDirectoryUser();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Fail closed: an unreachable or misbehaving gateway must never be treated as a successful login.
            logger.LogError(ex, "AD gateway authenticate call failed for {User}.", username);
            return null;
        }
    }

    public async Task<DirectoryUser?> FindByAccountNameAsync(string accountName, CancellationToken cancellationToken = default)
        => (await GetUserAsync(_options.LookupPath.Replace("{account}", Uri.EscapeDataString(accountName)), cancellationToken))?.ToDirectoryUser();

    public async Task<bool> IsAccountEnabledAsync(string objectSid, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(_options.StatusPath.Replace("{sid}", Uri.EscapeDataString(objectSid)), cancellationToken);
        return user is { Enabled: true };
    }

    public async Task<bool> IsPermittedToProvisionAsync(
        DirectoryUser user, string? filterOuDn, string? filterGroupSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filterOuDn) && string.IsNullOrWhiteSpace(filterGroupSid))
        {
            return true;
        }

        // Re-read the user through the gateway to obtain the authoritative group/DN set for the coarse filter.
        var full = await GetUserAsync(_options.LookupPath.Replace("{account}", Uri.EscapeDataString(user.SamAccountName)), cancellationToken);
        if (full is null)
        {
            return false;
        }

        // OU filter: the gateway's distinguishedName (if provided) must sit under the configured OU DN.
        if (!string.IsNullOrWhiteSpace(filterOuDn)
            && (full.DistinguishedName is null || !full.DistinguishedName.EndsWith(filterOuDn, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Group filter: the configured group identifier must appear in the user's returned AD groups (exact match,
        // case-insensitive) — a group SID, DN or name, whichever the gateway emits.
        if (!string.IsNullOrWhiteSpace(filterGroupSid)
            && !(full.Groups ?? []).Contains(filterGroupSid, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private async Task<AdRestUser?> GetUserAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(BuildUrl(path), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AD gateway lookup {Path} returned {Status}.", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AdRestUser>(Json, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "AD gateway lookup {Path} failed.", path);
            return null;
        }
    }

    private string BuildUrl(string path) => $"{_options.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private sealed record AuthRequest(string Username, string Password);

    /// <summary>The gateway's user payload (the documented AD-REST contract). Property matching is case-insensitive.</summary>
    private sealed record AdRestUser(
        [property: JsonPropertyName("samAccountName")] string? SamAccountName,
        [property: JsonPropertyName("userPrincipalName")] string? UserPrincipalName,
        [property: JsonPropertyName("objectSid")] string? ObjectSid,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("distinguishedName")] string? DistinguishedName,
        [property: JsonPropertyName("groups")] IReadOnlyList<string>? Groups)
    {
        public DirectoryUser ToDirectoryUser() => new(
            SamAccountName ?? string.Empty,
            UserPrincipalName ?? string.Empty,
            ObjectSid ?? string.Empty,
            Email ?? string.Empty,
            FirstName ?? string.Empty,
            LastName ?? string.Empty,
            DisplayName,
            Enabled);
    }
}
