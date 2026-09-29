using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Identity;

/// <summary>
/// A single-use, expiring token backing an emailed set-password (invite) or reset link. Only a hash of the
/// raw token is stored — the raw value lives solely in the emailed link and is never persisted. Consuming a
/// token is idempotency-protected: it can be redeemed once, before it expires.
/// </summary>
public sealed class PasswordResetToken : Entity
{
    private PasswordResetToken()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 (hex) of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; private set; } = null!;

    public CredentialTokenPurpose Purpose { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static PasswordResetToken Issue(Guid userId, string tokenHash, CredentialTokenPurpose purpose, DateTimeOffset expiresAt)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = Guard.NotNullOrWhiteSpace(tokenHash, "credential.token_hash_required", "A token hash is required."),
            Purpose = purpose,
            ExpiresAt = expiresAt,
        };

    /// <summary>True when the token is neither consumed nor expired at <paramref name="nowUtc"/>.</summary>
    public bool IsRedeemable(DateTimeOffset nowUtc) => ConsumedAt is null && ExpiresAt > nowUtc;

    /// <summary>Mark the token used. Rejects an already-consumed or expired token.</summary>
    public void Consume(DateTimeOffset nowUtc)
    {
        Guard.Against(ConsumedAt is not null, "credential.token_consumed", "This link has already been used.");
        Guard.Against(ExpiresAt <= nowUtc, "credential.token_expired", "This link has expired.");
        ConsumedAt = nowUtc;
    }
}
