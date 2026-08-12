using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Domain.Tests.Exceptions;

public sealed class RootCauseGapTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static RootCauseGap Open() => RootCauseGap.Open("No SoD policy", "desc", "process_gap", Owner, null, Actor, Now);

    [Fact]
    public void Opens_in_open_status()
    {
        var gap = Open();
        Assert.Equal(RootCauseGapStatus.Open, gap.Status);
        Assert.False(gap.IsClosed);
    }

    [Fact]
    public void Close_requires_a_rationale_of_at_least_10_chars()
    {
        var gap = Open();
        Assert.Throws<DomainException>(() => gap.Close("short", Actor, Now));
        gap.Close("Policy rolled out group-wide", Actor, Now);
        Assert.True(gap.IsClosed);
        Assert.Equal(Actor, gap.ClosedByUserId);
    }

    [Fact]
    public void A_closed_gap_rejects_edits_and_links_until_reopened()
    {
        var gap = Open();
        gap.Close("Policy rolled out group-wide", Actor, Now);
        Assert.Throws<InvalidStateTransitionException>(() => gap.UpdateDetails("x", null, null, Owner, null));
        Assert.Throws<InvalidStateTransitionException>(() => gap.LinkException(Guid.NewGuid(), Actor));

        gap.Reopen();
        Assert.Equal(RootCauseGapStatus.Open, gap.Status);
        Assert.Null(gap.ClosureRationale);
    }

    [Fact]
    public void Linking_the_same_exception_twice_is_idempotent()
    {
        var gap = Open();
        var exceptionId = Guid.NewGuid();
        gap.LinkException(exceptionId, Actor);
        gap.LinkException(exceptionId, Actor);
        Assert.Single(gap.Links);
    }

    [Fact]
    public void Reopening_an_open_gap_is_rejected()
        => Assert.Throws<InvalidStateTransitionException>(() => Open().Reopen());
}
