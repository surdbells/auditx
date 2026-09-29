using AuditX.Domain.Common;

namespace AuditX.Domain.Identity;

/// <summary>
/// A user's local password credential (1:1 with a user, keyed by <see cref="UserId"/>). AuditX stores only
/// the salted, iterated hash — never the password. Tracks the must-change flag, when the password was last
/// set (for expiry), lockout counters, a rotating security stamp, and a credentials-changed timestamp used
/// to invalidate sessions issued before the last change.
/// </summary>
public sealed class UserCredential : AggregateRoot
{
    private UserCredential()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>The self-describing PBKDF2 hash string. Never the plaintext password.</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>Rotated whenever the password changes; embedded in issued sessions to detect stale tokens.</summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>When true, the user must set a new password before doing anything else.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>When the current password was set — the basis for expiry.</summary>
    public DateTimeOffset PasswordSetAt { get; private set; }

    /// <summary>The most recent moment the credential materially changed; sessions issued earlier are invalid.</summary>
    public DateTimeOffset CredentialsChangedAt { get; private set; }

    /// <summary>Consecutive failed sign-in attempts since the last success or lockout reset.</summary>
    public int FailedAccessCount { get; private set; }

    /// <summary>When set and in the future, the account is locked out until this instant.</summary>
    public DateTimeOffset? LockoutEndAt { get; private set; }

    public static UserCredential Create(Guid userId, string passwordHash, bool mustChangePassword, DateTimeOffset nowUtc)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PasswordHash = Guard.NotNullOrWhiteSpace(passwordHash, "credential.hash_required", "A password hash is required."),
            SecurityStamp = Guid.NewGuid(),
            MustChangePassword = mustChangePassword,
            PasswordSetAt = nowUtc,
            CredentialsChangedAt = nowUtc,
        };

    /// <summary>
    /// Replace the stored hash (a password change or reset). Rotates the security stamp (invalidating existing
    /// sessions), resets lockout/failure tracking, and stamps the change time.
    /// </summary>
    public void SetPassword(string passwordHash, bool mustChangePassword, DateTimeOffset nowUtc)
    {
        PasswordHash = Guard.NotNullOrWhiteSpace(passwordHash, "credential.hash_required", "A password hash is required.");
        SecurityStamp = Guid.NewGuid();
        MustChangePassword = mustChangePassword;
        PasswordSetAt = nowUtc;
        CredentialsChangedAt = nowUtc;
        FailedAccessCount = 0;
        LockoutEndAt = null;
    }

    /// <summary>Transparently upgrade the stored hash to stronger parameters after a successful verify — no policy change.</summary>
    public void UpgradeHash(string passwordHash)
        => PasswordHash = Guard.NotNullOrWhiteSpace(passwordHash, "credential.hash_required", "A password hash is required.");

    /// <summary>Force a password change at next sign-in (e.g. an admin reset that did not set a known password).</summary>
    public void RequireChange() => MustChangePassword = true;

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockoutEndAt is { } end && end > nowUtc;

    /// <summary>True when the password is older than the policy's expiry window (0 = never expires).</summary>
    public bool IsExpired(int expiryDays, DateTimeOffset nowUtc)
        => expiryDays > 0 && PasswordSetAt.AddDays(expiryDays) <= nowUtc;

    /// <summary>
    /// Record a failed sign-in. When a prior lockout window has already elapsed the counter restarts; once the
    /// failure count reaches <paramref name="maxAttempts"/> (when &gt; 0) the account locks for the cooldown window.
    /// </summary>
    public void RegisterFailedAttempt(int maxAttempts, int lockoutMinutes, DateTimeOffset nowUtc)
    {
        if (LockoutEndAt is { } end && end <= nowUtc)
        {
            FailedAccessCount = 0;
            LockoutEndAt = null;
        }

        FailedAccessCount++;
        if (maxAttempts > 0 && FailedAccessCount >= maxAttempts)
        {
            LockoutEndAt = nowUtc.AddMinutes(lockoutMinutes);
        }
    }

    /// <summary>Reset failure tracking after a successful sign-in.</summary>
    public void RegisterSuccessfulLogin()
    {
        FailedAccessCount = 0;
        LockoutEndAt = null;
    }

    /// <summary>Administratively clear a lockout and reset the failure counter.</summary>
    public void Unlock()
    {
        FailedAccessCount = 0;
        LockoutEndAt = null;
    }
}
