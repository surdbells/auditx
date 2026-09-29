using AuditX.Domain.Identity;

namespace AuditX.Domain.Tests.Identity;

public sealed class UserCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static UserCredential New(bool mustChange = false)
        => UserCredential.Create(Guid.NewGuid(), "PBKDF2$SHA512$210000$c2FsdA==$aGFzaA==", mustChange, Now);

    [Fact]
    public void Create_initialises_the_credential()
    {
        var c = New(mustChange: true);
        Assert.NotEqual(Guid.Empty, c.SecurityStamp);
        Assert.True(c.MustChangePassword);
        Assert.Equal(Now, c.PasswordSetAt);
        Assert.Equal(Now, c.CredentialsChangedAt);
        Assert.Equal(0, c.FailedAccessCount);
        Assert.Null(c.LockoutEndAt);
        Assert.False(c.IsLockedOut(Now));
    }

    [Fact]
    public void SetPassword_rotates_the_stamp_and_clears_lockout_and_must_change()
    {
        var c = New(mustChange: true);
        var stamp = c.SecurityStamp;
        c.RegisterFailedAttempt(3, 15, Now);

        var later = Now.AddMinutes(5);
        c.SetPassword("PBKDF2$SHA512$210000$bmV3$bmV3", mustChangePassword: false, later);

        Assert.NotEqual(stamp, c.SecurityStamp);
        Assert.False(c.MustChangePassword);
        Assert.Equal(later, c.PasswordSetAt);
        Assert.Equal(later, c.CredentialsChangedAt);
        Assert.Equal(0, c.FailedAccessCount);
        Assert.Null(c.LockoutEndAt);
    }

    [Fact]
    public void UpgradeHash_replaces_the_hash_without_rotating_the_stamp()
    {
        var c = New();
        var stamp = c.SecurityStamp;
        var setAt = c.PasswordSetAt;
        c.UpgradeHash("PBKDF2$SHA512$400000$c2FsdA==$YmV0dGVy");
        Assert.Equal(stamp, c.SecurityStamp);
        Assert.Equal(setAt, c.PasswordSetAt);
    }

    [Fact]
    public void RegisterFailedAttempt_locks_once_the_threshold_is_reached()
    {
        var c = New();
        c.RegisterFailedAttempt(3, 15, Now);
        c.RegisterFailedAttempt(3, 15, Now);
        Assert.False(c.IsLockedOut(Now));
        Assert.Equal(2, c.FailedAccessCount);

        c.RegisterFailedAttempt(3, 15, Now);
        Assert.True(c.IsLockedOut(Now));
        Assert.Equal(Now.AddMinutes(15), c.LockoutEndAt);
    }

    [Fact]
    public void Lockout_expires_and_the_counter_restarts()
    {
        var c = New();
        c.RegisterFailedAttempt(1, 15, Now); // locks immediately (threshold 1)
        Assert.True(c.IsLockedOut(Now));

        var afterWindow = Now.AddMinutes(16);
        Assert.False(c.IsLockedOut(afterWindow));
        c.RegisterFailedAttempt(3, 15, afterWindow); // window elapsed → count restarts at 1
        Assert.Equal(1, c.FailedAccessCount);
        Assert.False(c.IsLockedOut(afterWindow));
    }

    [Fact]
    public void MaxAttempts_zero_disables_lockout()
    {
        var c = New();
        for (var i = 0; i < 50; i++)
        {
            c.RegisterFailedAttempt(0, 15, Now);
        }

        Assert.False(c.IsLockedOut(Now));
        Assert.Equal(50, c.FailedAccessCount);
    }

    [Fact]
    public void Successful_login_and_unlock_reset_failure_tracking()
    {
        var c = New();
        c.RegisterFailedAttempt(3, 15, Now);
        c.RegisterSuccessfulLogin();
        Assert.Equal(0, c.FailedAccessCount);

        c.RegisterFailedAttempt(1, 15, Now);
        Assert.True(c.IsLockedOut(Now));
        c.Unlock();
        Assert.False(c.IsLockedOut(Now));
        Assert.Equal(0, c.FailedAccessCount);
    }

    [Theory]
    [InlineData(0, false)]   // 0 = never expires
    [InlineData(90, false)]  // within window
    [InlineData(30, true)]   // past window
    public void IsExpired_honours_the_expiry_window(int expiryDays, bool expected)
    {
        var c = New();
        var check = Now.AddDays(60);
        Assert.Equal(expected, c.IsExpired(expiryDays, check));
    }
}
