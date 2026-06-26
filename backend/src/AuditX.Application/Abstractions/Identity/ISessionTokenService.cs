namespace AuditX.Application.Abstractions.Identity;

/// <summary>An issued session JWT and the timestamps the SPA needs to manage the session.</summary>
public sealed record IssuedToken(
    string Token,
    string TokenId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt);

/// <summary>
/// Issues signed session JWTs after a successful AD authentication. Tokens carry a sliding expiry
/// (default 8h) capped by an absolute lifetime (default 24h) that cannot be extended.
/// </summary>
public interface ISessionTokenService
{
    IssuedToken Issue(Guid userId, IReadOnlyCollection<string> roleNames);

    /// <summary>Re-issue (slide) a token for an active session without extending the absolute lifetime.</summary>
    IssuedToken Refresh(Guid userId, IReadOnlyCollection<string> roleNames, DateTimeOffset absoluteExpiresAt);
}
