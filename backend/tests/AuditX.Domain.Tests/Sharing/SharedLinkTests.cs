using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Sharing;

namespace AuditX.Domain.Tests.Sharing;

public sealed class SharedLinkTests
{
    private static readonly Guid Target = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2027, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private static SharedLink New(DateTimeOffset? expiresAt = null)
        => SharedLink.Create("abc123", SharedLinkTargetType.Report, Target, User, expiresAt);

    [Fact]
    public void Create_requires_slug_target_and_creator()
    {
        Assert.Throws<DomainException>(() => SharedLink.Create("  ", SharedLinkTargetType.Report, Target, User, null));
        Assert.Throws<DomainException>(() => SharedLink.Create("abc", SharedLinkTargetType.Report, Guid.Empty, User, null));
        Assert.Throws<DomainException>(() => SharedLink.Create("abc", SharedLinkTargetType.Report, Target, Guid.Empty, null));

        var link = New();
        Assert.True(link.IsActive(Now));
        Assert.Null(link.RevokedAt);
    }

    [Fact]
    public void Revoke_deactivates_and_is_idempotent()
    {
        var link = New();
        link.Revoke(User, Now);
        Assert.False(link.IsActive(Now));
        Assert.Equal(Now, link.RevokedAt);

        // A second revoke keeps the original actor/time.
        link.Revoke(Guid.NewGuid(), Now.AddDays(1));
        Assert.Equal(User, link.RevokedByUserId);
        Assert.Equal(Now, link.RevokedAt);
    }

    [Fact]
    public void Expiry_deactivates_once_past_due()
    {
        var link = New(expiresAt: Now.AddDays(7));
        Assert.True(link.IsActive(Now));
        Assert.True(link.IsActive(Now.AddDays(6)));
        Assert.False(link.IsActive(Now.AddDays(7)));   // exactly at expiry is no longer active
        Assert.False(link.IsActive(Now.AddDays(30)));
    }

    [Fact]
    public void Soft_deleted_link_is_never_active()
    {
        var link = New();
        link.SoftDelete(User, Now);
        Assert.False(link.IsActive(Now));
    }
}
