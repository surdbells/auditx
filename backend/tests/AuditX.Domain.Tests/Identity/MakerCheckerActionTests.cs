using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Domain.Tests.Identity;

public sealed class MakerCheckerActionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static MakerCheckerAction Pending(Guid maker)
        => MakerCheckerAction.Submit("role_permission_change", "role", null, maker, "{}");

    [Fact]
    public void Submit_starts_pending()
    {
        var action = Pending(Guid.NewGuid());
        Assert.Equal(MakerCheckerStatus.Pending, action.Status);
    }

    [Fact]
    public void Approve_by_maker_is_rejected()
    {
        var maker = Guid.NewGuid();
        var action = Pending(maker);

        var ex = Assert.Throws<DomainException>(() => action.Approve(maker, Now));
        Assert.Equal("mc.self_approval_forbidden", ex.Code);
        Assert.Equal(MakerCheckerStatus.Pending, action.Status);
    }

    [Fact]
    public void Approve_by_checker_succeeds()
    {
        var action = Pending(Guid.NewGuid());
        var checker = Guid.NewGuid();

        action.Approve(checker, Now);

        Assert.Equal(MakerCheckerStatus.Approved, action.Status);
        Assert.Equal(checker, action.CheckerUserId);
        Assert.Equal(Now, action.ResolvedAt);
    }

    [Fact]
    public void Approve_twice_is_invalid()
    {
        var action = Pending(Guid.NewGuid());
        action.Approve(Guid.NewGuid(), Now);

        Assert.Throws<InvalidStateTransitionException>(() => action.Approve(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Reject_requires_reason_of_at_least_20_characters()
    {
        var action = Pending(Guid.NewGuid());

        Assert.Throws<DomainException>(() => action.Reject(Guid.NewGuid(), "too short", Now));
    }

    [Fact]
    public void Reject_with_valid_reason_succeeds()
    {
        var action = Pending(Guid.NewGuid());

        action.Reject(Guid.NewGuid(), "This change is out of policy and not approved.", Now);

        Assert.Equal(MakerCheckerStatus.Rejected, action.Status);
        Assert.NotNull(action.ResolutionComment);
    }
}
