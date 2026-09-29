using AuditX.Domain.Common;

namespace AuditX.Domain.Identity;

/// <summary>
/// A previously-used password hash for a user, retained so the policy can forbid reuse of the last N passwords.
/// The application layer trims history beyond the configured depth.
/// </summary>
public sealed class UserPasswordHistory : Entity
{
    private UserPasswordHistory()
    {
    }

    public Guid UserId { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public DateTimeOffset SetAt { get; private set; }

    public static UserPasswordHistory Record(Guid userId, string passwordHash, DateTimeOffset nowUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        PasswordHash = Guard.NotNullOrWhiteSpace(passwordHash, "credential.hash_required", "A password hash is required."),
        SetAt = nowUtc,
    };
}
