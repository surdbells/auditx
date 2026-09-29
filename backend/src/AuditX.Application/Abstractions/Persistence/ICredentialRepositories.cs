using AuditX.Domain.Identity;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence for local password credentials (1:1 with a user). Callers commit via the unit of work.</summary>
public interface IUserCredentialRepository
{
    Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(UserCredential credential);

    void Remove(UserCredential credential);
}

/// <summary>Persistence for the per-user password-history ring used to forbid reuse of recent passwords.</summary>
public interface IPasswordHistoryRepository
{
    /// <summary>The most recent <paramref name="count"/> history entries for a user, newest first.</summary>
    Task<IReadOnlyList<UserPasswordHistory>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default);

    void Add(UserPasswordHistory entry);

    void RemoveRange(IEnumerable<UserPasswordHistory> entries);
}

/// <summary>Persistence for single-use, expiring set-password / reset tokens (stored hashed).</summary>
public interface IPasswordResetTokenRepository
{
    /// <summary>Find a token by its stored hash (tracked), or null.</summary>
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>All not-yet-consumed tokens for a user (tracked) — used to invalidate outstanding links on reissue.</summary>
    Task<IReadOnlyList<PasswordResetToken>> GetUnconsumedForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(PasswordResetToken token);
}
