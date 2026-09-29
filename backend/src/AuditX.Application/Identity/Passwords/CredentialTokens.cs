using System.Security.Cryptography;

namespace AuditX.Application.Identity.Passwords;

/// <summary>
/// Generates and hashes one-time credential tokens (set-password / reset links). Only the SHA-256 hash is
/// stored; the raw token lives solely in the emailed link, so a database read cannot reveal a usable token.
/// </summary>
public static class CredentialTokens
{
    /// <summary>A URL-safe, high-entropy raw token (256 bits) for embedding in a link.</summary>
    public static string GenerateRawToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>Lower-case hex SHA-256 of the raw token, for storage and lookup.</summary>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
