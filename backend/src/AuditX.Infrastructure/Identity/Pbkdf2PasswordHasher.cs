using System.Globalization;
using System.Security.Cryptography;
using AuditX.Application.Abstractions.Identity;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// PBKDF2 (HMAC-SHA-512) password hasher. Produces a self-describing hash string
/// <c>PBKDF2$SHA512${iterations}${saltBase64}${keyBase64}</c> so the parameters travel with the hash and
/// can be upgraded over time. Chosen over Argon2/bcrypt to avoid a native/third-party dependency in an
/// air-gapped on-premises build; the format is versioned so the KDF can be strengthened later.
/// Comparisons are constant-time.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Scheme = "PBKDF2";
    private const string AlgorithmLabel = "SHA512";
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private static readonly HashAlgorithmName Prf = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Prf, KeyBytes);
        return string.Join(
            '$',
            Scheme,
            AlgorithmLabel,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public PasswordVerificationResult Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
        {
            return new PasswordVerificationResult(false, false);
        }

        var parts = hash.Split('$');
        if (parts.Length != 5 || parts[0] != Scheme)
        {
            return new PasswordVerificationResult(false, false);
        }

        var prf = parts[1] switch
        {
            "SHA512" => HashAlgorithmName.SHA512,
            "SHA256" => HashAlgorithmName.SHA256,
            _ => (HashAlgorithmName?)null,
        };
        if (prf is null
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return new PasswordVerificationResult(false, false);
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return new PasswordVerificationResult(false, false);
        }

        if (expected.Length == 0)
        {
            return new PasswordVerificationResult(false, false);
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, prf.Value, expected.Length);
        var matched = CryptographicOperations.FixedTimeEquals(actual, expected);
        var needsRehash = matched && (parts[1] != AlgorithmLabel || iterations < Iterations || expected.Length < KeyBytes);
        return new PasswordVerificationResult(matched, needsRehash);
    }
}
