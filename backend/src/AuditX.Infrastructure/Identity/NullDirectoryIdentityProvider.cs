using AuditX.Application.Abstractions.Identity;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Directory provider used when <c>Identity:Provider = Local</c> — there is no directory, so every directory
/// operation is a no-op. All authentication flows through the local-password path in the login handler.
/// </summary>
public sealed class NullDirectoryIdentityProvider : IIdentityProvider
{
    public Task<DirectoryUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        => Task.FromResult<DirectoryUser?>(null);

    public Task<DirectoryUser?> FindByAccountNameAsync(string accountName, CancellationToken cancellationToken = default)
        => Task.FromResult<DirectoryUser?>(null);

    public Task<bool> IsAccountEnabledAsync(string objectSid, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> IsPermittedToProvisionAsync(DirectoryUser user, string? filterOuDn, string? filterGroupSid, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
