using AuditX.Domain.Common;
using AuditX.Domain.Identity;

namespace AuditX.Domain.Tests.Identity;

public sealed class InstitutionSettingsPasswordPolicyTests
{
    private static InstitutionSettings New() => InstitutionSettings.CreateDefault("Acme");

    [Fact]
    public void Default_settings_ship_a_secure_local_password_policy_disabled_by_default()
    {
        var s = New();
        Assert.False(s.EnableLocalPasswords);
        Assert.Equal(12, s.PasswordMinLength);
        Assert.True(s.PasswordRequireUppercase);
        Assert.True(s.PasswordRequireSymbol);
        Assert.Equal(5, s.PasswordHistoryDepth);
        Assert.Equal(90, s.PasswordExpiryDays);
        Assert.Equal(5, s.PasswordMaxFailedAttempts);
        Assert.Equal(15, s.PasswordLockoutMinutes);
    }

    [Fact]
    public void SetPasswordPolicy_applies_valid_values()
    {
        var s = New();
        s.SetPasswordPolicy(true, 16, false, true, true, false, 10, 0, 0, 30);

        Assert.True(s.EnableLocalPasswords);
        Assert.Equal(16, s.PasswordMinLength);
        Assert.False(s.PasswordRequireUppercase);
        Assert.False(s.PasswordRequireSymbol);
        Assert.Equal(10, s.PasswordHistoryDepth);
        Assert.Equal(0, s.PasswordExpiryDays);
        Assert.Equal(0, s.PasswordMaxFailedAttempts);
        Assert.Equal(30, s.PasswordLockoutMinutes);
    }

    [Theory]
    [InlineData(7, 5, 90, 5, 15)]     // min length too small
    [InlineData(129, 5, 90, 5, 15)]   // min length too large
    [InlineData(12, 25, 90, 5, 15)]   // history too deep
    [InlineData(12, 5, 3651, 5, 15)]  // expiry too long
    [InlineData(12, 5, 90, 21, 15)]   // too many attempts
    [InlineData(12, 5, 90, 5, 0)]     // lockout below 1 minute
    [InlineData(12, 5, 90, 5, 1441)]  // lockout above 1440 minutes
    public void SetPasswordPolicy_rejects_out_of_range_values(
        int minLength, int historyDepth, int expiryDays, int maxFailedAttempts, int lockoutMinutes)
    {
        var s = New();
        Assert.Throws<DomainException>(() => s.SetPasswordPolicy(
            true, minLength, true, true, true, true, historyDepth, expiryDays, maxFailedAttempts, lockoutMinutes));
    }
}
