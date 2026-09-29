using AuditX.Infrastructure.Identity;

namespace AuditX.Infrastructure.Tests.Identity;

/// <summary>Pure unit tests for the PBKDF2 password hasher (no database — no Docker required).</summary>
public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_produces_the_self_describing_versioned_format()
    {
        var hash = _hasher.Hash("Str0ng&Pass!");
        var parts = hash.Split('$');
        Assert.Equal(5, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.Equal("SHA512", parts[1]);
        Assert.Equal("210000", parts[2]);
    }

    [Fact]
    public void Hashing_the_same_password_twice_yields_different_salts_and_hashes()
    {
        Assert.NotEqual(_hasher.Hash("Str0ng&Pass!"), _hasher.Hash("Str0ng&Pass!"));
    }

    [Fact]
    public void Verify_succeeds_for_the_correct_password_and_does_not_need_rehash()
    {
        var hash = _hasher.Hash("Str0ng&Pass!");
        var result = _hasher.Verify(hash, "Str0ng&Pass!");
        Assert.True(result.Succeeded);
        Assert.False(result.NeedsRehash);
    }

    [Fact]
    public void Verify_fails_for_the_wrong_password()
    {
        var hash = _hasher.Hash("Str0ng&Pass!");
        Assert.False(_hasher.Verify(hash, "wrong-password").Succeeded);
    }

    [Fact]
    public void Verify_fails_for_a_tampered_hash()
    {
        var hash = _hasher.Hash("Str0ng&Pass!");
        var parts = hash.Split('$');
        parts[4] = Convert.ToBase64String(new byte[32]); // zeroed derived key
        Assert.False(_hasher.Verify(string.Join('$', parts), "Str0ng&Pass!").Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2$SHA512$notanumber$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2$SHA512$210000$!!notbase64!!$aGFzaA==")]
    public void Verify_returns_false_for_malformed_hashes_without_throwing(string malformed)
    {
        Assert.False(_hasher.Verify(malformed, "Str0ng&Pass!").Succeeded);
    }

    [Fact]
    public void Verify_flags_needs_rehash_for_a_lower_iteration_count()
    {
        // A legitimately-derived hash at a weaker iteration count should still verify but request an upgrade.
        var salt = new byte[16];
        var key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            "Str0ng&Pass!", salt, 1000, System.Security.Cryptography.HashAlgorithmName.SHA512, 32);
        var legacy = string.Join('$', "PBKDF2", "SHA512", "1000", Convert.ToBase64String(salt), Convert.ToBase64String(key));

        var result = _hasher.Verify(legacy, "Str0ng&Pass!");
        Assert.True(result.Succeeded);
        Assert.True(result.NeedsRehash);
    }
}
