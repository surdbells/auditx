namespace AuditX.Application.Abstractions.Identity;

/// <summary>Directory-sourced attributes for a user, as asserted by Active Directory.</summary>
public sealed record DirectoryUser(
    string SamAccountName,
    string UserPrincipalName,
    string ObjectSid,
    string Email,
    string FirstName,
    string LastName,
    string? DisplayName,
    bool IsEnabled);

/// <summary>
/// Abstraction over the bank's Active Directory. Production uses LDAPS for lookup and a forms-fallback
/// LDAP bind for credential validation; the development provider returns seeded users so the platform
/// runs without a domain. AuditX never stores passwords.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>Validate credentials via an LDAP bind and return the directory user, or null if invalid.</summary>
    Task<DirectoryUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Look up a directory user by their sAMAccountName / principal name (used by SSO).</summary>
    Task<DirectoryUser?> FindByAccountNameAsync(string accountName, CancellationToken cancellationToken = default);

    /// <summary>Verify the directory account is still present and enabled (re-checked on session refresh).</summary>
    Task<bool> IsAccountEnabledAsync(string objectSid, CancellationToken cancellationToken = default);

    /// <summary>True if the directory user passes the optional coarse-grained provisioning filter (US-M1-006).</summary>
    Task<bool> IsPermittedToProvisionAsync(DirectoryUser user, string? filterOuDn, string? filterGroupSid, CancellationToken cancellationToken = default);
}
