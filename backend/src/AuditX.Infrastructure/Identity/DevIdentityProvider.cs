using AuditX.Application.Abstractions.Identity;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Development identity provider backed by the fixed <see cref="DevUsers"/> set. Lets the platform run
/// and be demonstrated without a domain. Must never be enabled in production.
/// </summary>
public sealed class DevIdentityProvider : IIdentityProvider
{
    public Task<DirectoryUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = Find(username);
        if (user is null || !string.Equals(user.Password, password, StringComparison.Ordinal))
        {
            return Task.FromResult<DirectoryUser?>(null);
        }

        return Task.FromResult<DirectoryUser?>(user.ToDirectoryUser());
    }

    public Task<DirectoryUser?> FindByAccountNameAsync(string accountName, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(accountName)?.ToDirectoryUser());

    public Task<bool> IsAccountEnabledAsync(string objectSid, CancellationToken cancellationToken = default)
        => Task.FromResult(DevUsers.All.Any(u => u.ObjectSid == objectSid));

    public Task<bool> IsPermittedToProvisionAsync(DirectoryUser user, string? filterOuDn, string? filterGroupSid, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    private static DevUser? Find(string identifier)
        => DevUsers.All.FirstOrDefault(u =>
            string.Equals(u.SamAccountName, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.UserPrincipalName, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Email, identifier, StringComparison.OrdinalIgnoreCase));
}
