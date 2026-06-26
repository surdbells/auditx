using System.DirectoryServices.Protocols;
using System.Net;
using AuditX.Application.Abstractions.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Active Directory identity provider. Uses LDAPS (mandatory; plaintext LDAP is unsupported) for
/// directory lookup with a service account, and validates forms-login credentials with a per-user LDAP
/// bind. AuditX never stores passwords.
/// </summary>
public sealed class ActiveDirectoryIdentityProvider(
    IOptions<ActiveDirectoryOptions> options,
    ILogger<ActiveDirectoryIdentityProvider> logger)
    : IIdentityProvider
{
    private static readonly string[] UserAttributes =
        ["sAMAccountName", "userPrincipalName", "objectSid", "mail", "givenName", "sn", "displayName", "userAccountControl", "distinguishedName"];

    private const int AccountDisabledFlag = 0x2;

    private readonly ActiveDirectoryOptions _options = options.Value;

    public Task<DirectoryUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var bindName = ToBindName(username);
        try
        {
            using var connection = CreateConnection();
            connection.AuthType = AuthType.Basic;
            connection.Bind(new NetworkCredential(bindName, password));
        }
        catch (LdapException ex)
        {
            logger.LogInformation("LDAP bind failed for {User}: {Code}", username, ex.ErrorCode);
            return Task.FromResult<DirectoryUser?>(null);
        }

        // Credentials are valid; read the user's attributes with the service account.
        var filter = $"(|(sAMAccountName={EscapeFilter(StripDomain(username))})(userPrincipalName={EscapeFilter(username)}))";
        return Task.FromResult(SearchUser(filter));
    }

    public Task<DirectoryUser?> FindByAccountNameAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var filter = $"(|(sAMAccountName={EscapeFilter(StripDomain(accountName))})(userPrincipalName={EscapeFilter(accountName)}))";
        return Task.FromResult(SearchUser(filter));
    }

    public Task<bool> IsAccountEnabledAsync(string objectSid, CancellationToken cancellationToken = default)
    {
        var filter = $"(objectSid={SidConverter.ToLdapFilterValue(objectSid)})";
        var user = SearchUser(filter);
        return Task.FromResult(user is { IsEnabled: true });
    }

    public Task<bool> IsPermittedToProvisionAsync(DirectoryUser user, string? filterOuDn, string? filterGroupSid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filterOuDn) && string.IsNullOrWhiteSpace(filterGroupSid))
        {
            return Task.FromResult(true);
        }

        using var connection = CreateConnection();
        BindServiceAccount(connection);

        var entry = SearchOne(connection, $"(objectSid={SidConverter.ToLdapFilterValue(user.ObjectSid)})", ["distinguishedName", "memberOf"]);
        if (entry is null)
        {
            return Task.FromResult(false);
        }

        if (!string.IsNullOrWhiteSpace(filterOuDn))
        {
            var dn = GetAttribute(entry, "distinguishedName");
            if (dn is null || !dn.EndsWith(filterOuDn, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(filterGroupSid))
        {
            var groupEntry = SearchOne(connection, $"(objectSid={SidConverter.ToLdapFilterValue(filterGroupSid)})", ["distinguishedName"]);
            var groupDn = groupEntry is null ? null : GetAttribute(groupEntry, "distinguishedName");
            var memberOf = GetAttributeValues(entry, "memberOf");
            if (groupDn is null || !memberOf.Contains(groupDn, StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    private DirectoryUser? SearchUser(string filter)
    {
        using var connection = CreateConnection();
        BindServiceAccount(connection);
        var entry = SearchOne(connection, filter, UserAttributes);
        return entry is null ? null : MapUser(entry);
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_options.Host, _options.Port);
        var connection = new LdapConnection(identifier);
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseLdaps;
        connection.Timeout = TimeSpan.FromSeconds(30);
        return connection;
    }

    private void BindServiceAccount(LdapConnection connection)
    {
        connection.AuthType = AuthType.Basic;
        connection.Bind(new NetworkCredential(_options.ServiceAccountDn, _options.ServiceAccountPassword));
    }

    private SearchResultEntry? SearchOne(LdapConnection connection, string filter, string[] attributes)
    {
        var request = new SearchRequest(_options.BaseDn, filter, SearchScope.Subtree, attributes);
        var response = (SearchResponse)connection.SendRequest(request);
        return response.Entries.Count > 0 ? response.Entries[0] : null;
    }

    private static DirectoryUser MapUser(SearchResultEntry entry)
    {
        var uac = GetAttribute(entry, "userAccountControl");
        var enabled = uac is null || (int.TryParse(uac, out var flags) && (flags & AccountDisabledFlag) == 0);
        var sidBytes = entry.Attributes["objectSid"]?[0] as byte[] ?? [];

        return new DirectoryUser(
            GetAttribute(entry, "sAMAccountName") ?? string.Empty,
            GetAttribute(entry, "userPrincipalName") ?? string.Empty,
            sidBytes.Length > 0 ? SidConverter.ToStringSid(sidBytes) : string.Empty,
            GetAttribute(entry, "mail") ?? string.Empty,
            GetAttribute(entry, "givenName") ?? string.Empty,
            GetAttribute(entry, "sn") ?? string.Empty,
            GetAttribute(entry, "displayName"),
            enabled);
    }

    private static string? GetAttribute(SearchResultEntry entry, string name)
        => entry.Attributes.Contains(name) && entry.Attributes[name].Count > 0
            ? entry.Attributes[name][0]?.ToString()
            : null;

    private static IReadOnlyList<string> GetAttributeValues(SearchResultEntry entry, string name)
    {
        if (!entry.Attributes.Contains(name))
        {
            return [];
        }

        var values = new List<string>();
        foreach (var value in entry.Attributes[name].GetValues(typeof(string)))
        {
            if (value?.ToString() is { } s)
            {
                values.Add(s);
            }
        }

        return values;
    }

    private string ToBindName(string username)
    {
        if (username.Contains('\\') || username.Contains('@'))
        {
            return username;
        }

        return string.IsNullOrWhiteSpace(_options.UpnSuffix) ? username : $"{username}@{_options.UpnSuffix}";
    }

    private static string StripDomain(string username)
    {
        var slash = username.IndexOf('\\');
        if (slash >= 0)
        {
            return username[(slash + 1)..];
        }

        var at = username.IndexOf('@');
        return at >= 0 ? username[..at] : username;
    }

    private static string EscapeFilter(string value)
        => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
