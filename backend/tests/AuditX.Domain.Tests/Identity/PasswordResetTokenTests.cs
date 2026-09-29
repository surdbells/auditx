using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Domain.Tests.Identity;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static PasswordResetToken Issue(CredentialTokenPurpose purpose = CredentialTokenPurpose.Reset)
        => PasswordResetToken.Issue(Guid.NewGuid(), "abc123hash", purpose, Now.AddHours(1));

    [Fact]
    public void Issued_token_is_redeemable_before_expiry()
    {
        var t = Issue();
        Assert.True(t.IsRedeemable(Now));
        Assert.Null(t.ConsumedAt);
    }

    [Fact]
    public void Consume_marks_it_used_once()
    {
        var t = Issue();
        t.Consume(Now);
        Assert.Equal(Now, t.ConsumedAt);
        Assert.False(t.IsRedeemable(Now));
        Assert.Throws<DomainException>(() => t.Consume(Now));
    }

    [Fact]
    public void Expired_token_cannot_be_consumed_and_is_not_redeemable()
    {
        var t = Issue();
        var afterExpiry = Now.AddHours(2);
        Assert.False(t.IsRedeemable(afterExpiry));
        Assert.Throws<DomainException>(() => t.Consume(afterExpiry));
    }
}
