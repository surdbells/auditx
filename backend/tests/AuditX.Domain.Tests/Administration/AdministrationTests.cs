using AuditX.Domain.Administration;
using AuditX.Domain.Common;

namespace AuditX.Domain.Tests.Administration;

public sealed class SupportChannelSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Enable_requires_at_least_one_engineer()
        => Assert.Throws<DomainException>(() => SupportChannelSession.Enable([], Guid.NewGuid(), Now, 60));

    [Fact]
    public void Duration_is_capped()
    {
        var session = SupportChannelSession.Enable(["eng1"], Guid.NewGuid(), Now, durationMinutes: 100_000);
        Assert.True(session.ExpiresAt <= Now.AddMinutes(SupportChannelSession.MaxDurationMinutes));
    }

    [Fact]
    public void Active_within_window_then_revoked()
    {
        var session = SupportChannelSession.Enable(["eng1"], Guid.NewGuid(), Now, 60);
        Assert.True(session.IsActiveAt(Now.AddMinutes(30)));
        Assert.False(session.IsActiveAt(Now.AddMinutes(61)));

        session.Revoke(Guid.NewGuid(), Now.AddMinutes(10));
        Assert.False(session.IsActiveAt(Now.AddMinutes(20)));
    }
}

public sealed class ObjectRestoreRequestTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static ObjectRestoreRequest New(Guid requester)
        => ObjectRestoreRequest.Create("audit", Guid.NewGuid(), Now, "Restoring an audit deleted in error during cleanup.", requester);

    [Fact]
    public void Justification_must_be_long_enough()
    {
        Assert.Throws<DomainException>(() =>
            ObjectRestoreRequest.Create("audit", Guid.NewGuid(), Now, "too short", Guid.NewGuid()));
    }

    [Fact]
    public void Requester_cannot_approve_their_own_request()
    {
        var requester = Guid.NewGuid();
        var request = New(requester);
        Assert.Throws<DomainException>(() => request.Approve(requester));
    }

    [Fact]
    public void Approve_then_execute()
    {
        var request = New(Guid.NewGuid());
        request.Approve(Guid.NewGuid());
        Assert.Equal(Domain.Enums.ObjectRestoreStatus.Approved, request.Status);
        request.MarkExecuted();
        Assert.Equal(Domain.Enums.ObjectRestoreStatus.Executed, request.Status);
    }

    [Fact]
    public void Cannot_execute_unapproved_request()
        => Assert.Throws<InvalidStateTransitionException>(() => New(Guid.NewGuid()).MarkExecuted());
}
