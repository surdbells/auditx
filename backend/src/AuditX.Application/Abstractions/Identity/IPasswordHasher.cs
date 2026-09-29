namespace AuditX.Application.Abstractions.Identity;

/// <summary>The outcome of verifying a candidate password against a stored hash.</summary>
/// <param name="Succeeded">True when the candidate matches the stored hash.</param>
/// <param name="NeedsRehash">
/// True when the match succeeded but the stored hash uses outdated parameters (older algorithm or fewer
/// iterations) and should be transparently upgraded on the next successful sign-in.
/// </param>
public readonly record struct PasswordVerificationResult(bool Succeeded, bool NeedsRehash);

/// <summary>
/// Hashes and verifies local (institution-managed) passwords. AuditX stores only the salted, iterated,
/// self-describing hash string — never the password itself. Implementations must compare in constant time.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produce a versioned, self-describing hash (algorithm + parameters + salt + derived key) for storage.</summary>
    string Hash(string password);

    /// <summary>Verify a candidate against a stored hash in constant time, flagging whether the hash needs upgrading.</summary>
    PasswordVerificationResult Verify(string hash, string password);
}
