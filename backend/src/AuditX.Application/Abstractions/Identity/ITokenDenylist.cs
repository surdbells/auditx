namespace AuditX.Application.Abstractions.Identity;

/// <summary>
/// Server-side denylist of revoked session tokens (Redis-backed). Entries live until the token's
/// natural expiry. Used by logout, force-logout, and AD-disablement detection.
/// </summary>
public interface ITokenDenylist
{
    Task RevokeAsync(string tokenId, DateTimeOffset tokenExpiresAt, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default);
}
