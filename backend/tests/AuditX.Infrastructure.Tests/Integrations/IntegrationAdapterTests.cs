using System.Security.Cryptography;
using System.Text;
using AuditX.Infrastructure.Administration;
using AuditX.Infrastructure.Integrations;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Tests.Integrations;

public sealed class InternalNetworkPolicyTests
{
    private readonly InternalNetworkPolicy _policy = new();

    [Theory]
    [InlineData("http://10.0.0.5/hook", true)]
    [InlineData("https://172.16.4.9:8443/hook", true)]
    [InlineData("https://192.168.1.20/hook", true)]
    [InlineData("http://localhost/hook", true)]
    [InlineData("https://grc.internal/hook", true)]
    [InlineData("https://intranet/hook", true)]
    [InlineData("https://example.com/hook", false)]
    [InlineData("https://8.8.8.8/hook", false)]
    [InlineData("ftp://10.0.0.1/x", false)]
    public void Classifies_internal_versus_external(string url, bool expectedInternal)
        => Assert.Equal(expectedInternal, _policy.IsInternal(url));
}

public sealed class RsaReleasePackageVerifierTests
{
    [Fact]
    public void Valid_signature_and_matching_hash_verifies()
    {
        using var rsa = RSA.Create(2048);
        var manifest = Encoding.UTF8.GetBytes("manifest-content-v2.1.0");
        var signature = rsa.SignData(manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var hash = Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant();

        var verifier = new RsaReleasePackageVerifier(Microsoft.Extensions.Options.Options.Create(new ReleaseSigningOptions { PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem() }));

        var result = verifier.Verify("2.1.0", hash, signature, manifest);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Tampered_manifest_fails()
    {
        using var rsa = RSA.Create(2048);
        var manifest = Encoding.UTF8.GetBytes("manifest-content");
        var signature = rsa.SignData(manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var hash = Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant();
        var verifier = new RsaReleasePackageVerifier(Microsoft.Extensions.Options.Options.Create(new ReleaseSigningOptions { PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem() }));

        // Same declared hash, but the actual content was tampered with.
        var tampered = Encoding.UTF8.GetBytes("manifest-content-TAMPERED");
        var result = verifier.Verify("2.1.0", hash, signature, tampered);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Wrong_key_fails_signature()
    {
        using var signingKey = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var manifest = Encoding.UTF8.GetBytes("manifest");
        var signature = signingKey.SignData(manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var hash = Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant();
        var verifier = new RsaReleasePackageVerifier(Microsoft.Extensions.Options.Options.Create(new ReleaseSigningOptions { PublicKeyPem = otherKey.ExportSubjectPublicKeyInfoPem() }));

        Assert.False(verifier.Verify("1.0.0", hash, signature, manifest).IsValid);
    }
}
