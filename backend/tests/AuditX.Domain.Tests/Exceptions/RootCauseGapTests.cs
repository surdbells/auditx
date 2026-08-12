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

    [Fact]
    public void Remediation_actions_carry_an_owner_and_move_open_to_completed()
    {
        var gap = Open();
        var itemOwner = Guid.NewGuid();
        var item = gap.AddRemediation("Draft and approve an SoD policy", itemOwner, new DateOnly(2027, 6, 30), Actor, Now);

        Assert.Single(gap.Remediations);
        Assert.Equal(itemOwner, item.OwnerUserId);
        Assert.Equal(RootCauseGapRemediationStatus.Open, item.Status);

        gap.CompleteRemediation(item.Id, "Policy signed off by the board", Actor, Now);
        Assert.Equal(RootCauseGapRemediationStatus.Completed, item.Status);
        Assert.Equal(Actor, item.CompletedByUserId);

        gap.ReopenRemediation(item.Id);
        Assert.Equal(RootCauseGapRemediationStatus.Open, item.Status);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public void Adding_a_remediation_requires_an_owner_and_a_description()
    {
        var gap = Open();
        Assert.Throws<DomainException>(() => gap.AddRemediation("do a thing", Guid.Empty, null, Actor, Now));
        Assert.Throws<DomainException>(() => gap.AddRemediation("  ", Owner, null, Actor, Now));
    }

    [Fact]
    public void A_closed_gap_rejects_remediation_changes()
    {
        var gap = Open();
        var item = gap.AddRemediation("Fix it", Owner, null, Actor, Now);
        gap.Close("Policy rolled out group-wide", Actor, Now);

        Assert.Throws<InvalidStateTransitionException>(() => gap.AddRemediation("another", Owner, null, Actor, Now));
        Assert.Throws<InvalidStateTransitionException>(() => gap.CompleteRemediation(item.Id, null, Actor, Now));
        Assert.Throws<InvalidStateTransitionException>(() => gap.RemoveRemediation(item.Id));
    }

    [Fact]
    public void Completing_an_already_completed_remediation_is_rejected()
    {
        var gap = Open();
        var item = gap.AddRemediation("Fix it", Owner, null, Actor, Now);
        gap.CompleteRemediation(item.Id, null, Actor, Now);
        Assert.Throws<InvalidStateTransitionException>(() => gap.CompleteRemediation(item.Id, null, Actor, Now));
    }
}
