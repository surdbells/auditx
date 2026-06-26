using System.Security.Cryptography;
using AuditX.Application.Abstractions.Administration;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Administration;

/// <summary>ITANDT release-signing configuration (bound from <c>ReleaseSigning</c>).</summary>
public sealed class ReleaseSigningOptions
{
    public const string SectionName = "ReleaseSigning";

    /// <summary>PEM-encoded RSA public key used to verify release package signatures.</summary>
    public string PublicKeyPem { get; set; } = string.Empty;
}

/// <summary>
/// Verifies a signed offline release package: the manifest SHA-256 matches the supplied content, and
/// the RSA signature over the manifest validates against ITANDT's release-signing public key. Any
/// mismatch (tampering) fails verification (US-M15-032).
/// </summary>
public sealed class RsaReleasePackageVerifier(IOptions<ReleaseSigningOptions> options) : IReleasePackageVerifier
{
    private readonly ReleaseSigningOptions _options = options.Value;

    public ReleaseVerificationResult Verify(string version, string manifestSha256, byte[] signature, byte[] manifestContent)
    {
        var computed = Convert.ToHexString(SHA256.HashData(manifestContent)).ToLowerInvariant();
        if (!string.Equals(computed, manifestSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return new ReleaseVerificationResult(false, "Manifest hash does not match the package content.");
        }

        if (string.IsNullOrWhiteSpace(_options.PublicKeyPem))
        {
            return new ReleaseVerificationResult(false, "No release-signing public key is configured.");
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_options.PublicKeyPem);
            var valid = rsa.VerifyData(manifestContent, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return valid
                ? new ReleaseVerificationResult(true, $"Release {version} signature verified.")
                : new ReleaseVerificationResult(false, "Package signature is invalid.");
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return new ReleaseVerificationResult(false, "Signature verification failed: " + ex.Message);
        }
    }
}

/// <summary>Reads aggregate deployment metrics for the system-health surface.</summary>
public sealed class SystemMetricsProvider(AppDbContext db) : ISystemMetricsProvider
{
    public async Task<SystemMetrics> GetAsync(CancellationToken cancellationToken = default)
    {
        var total = await db.Users.CountAsync(cancellationToken);
        var active = await db.Users.CountAsync(u => u.Status == UserStatus.Active, cancellationToken);
        var templates = await db.Templates.CountAsync(cancellationToken);
        var integrations = await db.Integrations.CountAsync(i => i.IsActive, cancellationToken);
        return new SystemMetrics(active, total, templates, integrations);
    }
}
