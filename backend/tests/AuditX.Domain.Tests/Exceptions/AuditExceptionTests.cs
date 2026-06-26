using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Exceptions.Events;

namespace AuditX.Domain.Tests.Exceptions;

public sealed class AuditExceptionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Target = new(2027, 3, 1);
    private static readonly Guid AuditId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Raiser = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static AuditException New(ExceptionSeverity severity = ExceptionSeverity.Medium) => AuditException.Raise(
        AuditId, ItemId, Guid.NewGuid(), "Cash control gap", severity, "no segregation", "add maker-checker",
        "controls", Owner, Raiser, Target, targetDateOverridden: false, null, isRecurrence: false, null, "{}", Now);

    private static AuditException Approved()
    {
        var e = New();
        e.AddMapAction("Implement maker-checker", Owner, Target.AddDays(-5), "screenshot");
        e.SubmitMap(Owner, Now);
        e.ApproveMap(Actor, Now);
        return e;
    }

    [Fact]
    public void Raise_starts_open_and_emits_event()
    {
        var e = New();
        Assert.Equal(ExceptionStatus.Open, e.Status);
        Assert.Contains(e.DomainEvents, x => x is ExceptionRaisedEvent);
    }

    [Fact]
    public void Severity_change_requires_a_reason()
    {
        var e = New();
        Assert.Throws<DomainException>(() => e.ChangeSeverity(ExceptionSeverity.High, "too short", Actor));
        e.ChangeSeverity(ExceptionSeverity.High, "Escalated after repeat occurrence in the same branch.", Actor);
        Assert.Equal(ExceptionSeverity.High, e.Severity);
    }

    [Fact]
    public void Submit_map_requires_an_action_and_actions_within_target()
    {
        var empty = New();
        Assert.Throws<DomainException>(() => empty.SubmitMap(Owner, Now));

        var late = New();
        Assert.Throws<DomainException>(() => late.AddMapAction("x", Owner, Target.AddDays(5), null));

        var ok = New();
        ok.AddMapAction("Fix it", Owner, Target.AddDays(-1), null);
        ok.SubmitMap(Owner, Now);
        Assert.Equal(ExceptionStatus.MapSubmitted, ok.Status);
    }

    [Fact]
    public void Reject_then_resubmit_cycles_back_to_submitted()
    {
        var e = New();
        e.AddMapAction("Fix", Owner, Target.AddDays(-1), null);
        e.SubmitMap(Owner, Now);
        e.RejectMap("Insufficient detail; specify the control owner and date.", Actor);
        Assert.Equal(ExceptionStatus.MapRejected, e.Status);

        e.AddMapAction("Fix v2", Owner, Target.AddDays(-1), null);
        e.SubmitMap(Owner, Now);
        Assert.Equal(ExceptionStatus.MapSubmitted, e.Status);
    }

    [Fact]
    public void Action_completion_requires_evidence_then_map_completes()
    {
        var e = Approved();
        var actionId = e.MapActions[0].Id;
        Assert.Throws<DomainException>(() => e.MarkMapActionComplete(actionId, requireEvidence: true, hasEvidence: false, Owner, Now));

        e.MarkMapActionComplete(actionId, requireEvidence: true, hasEvidence: true, Owner, Now);
        e.MarkMapComplete(Owner);
        Assert.Equal(ExceptionStatus.PendingClosure, e.Status);
    }

    [Fact]
    public void Non_critical_close_closes_directly()
    {
        var e = Approved();
        e.MarkMapActionComplete(e.MapActions[0].Id, true, true, Owner, Now);
        e.MarkMapComplete(Owner);
        e.Close("verified", Actor, Now);
        Assert.Equal(ExceptionStatus.Closed, e.Status);
        Assert.False(e.CiaPending);
    }

    [Fact]
    public void Critical_close_holds_for_cia_then_countersign_closes()
    {
        var e = Approved2(ExceptionSeverity.Critical);
        e.Close("verified", Actor, Now);
        Assert.Equal(ExceptionStatus.PendingClosure, e.Status);
        Assert.True(e.CiaPending);
        Assert.Contains(e.DomainEvents, x => x is ExceptionPendingCiaEvent);

        var cia = Guid.NewGuid();
        e.CiaCountersign(cia, Now);
        Assert.Equal(ExceptionStatus.Closed, e.Status);
        Assert.False(e.CiaPending);
        Assert.Equal(cia, e.CiaCountersignedBy);
    }

    private static AuditException Approved2(ExceptionSeverity severity)
    {
        var e = New(severity);
        e.AddMapAction("Fix", Owner, Target.AddDays(-5), null);
        e.SubmitMap(Owner, Now);
        e.ApproveMap(Actor, Now);
        e.MarkMapActionComplete(e.MapActions[0].Id, true, true, Owner, Now);
        e.MarkMapComplete(Owner);
        return e;
    }

    [Fact]
    public void Return_for_evidence_reopens_to_map_approved()
    {
        var e = Approved2(ExceptionSeverity.Medium);
        e.ReturnForEvidence("Need the signed control matrix attached before closure.", Actor);
        Assert.Equal(ExceptionStatus.MapApproved, e.Status);
    }

    [Fact]
    public void Cancel_requires_reason_and_blocks_when_terminal()
    {
        var e = New();
        Assert.Throws<DomainException>(() => e.Cancel("short", Actor, Now));
        e.Cancel("Raised in error; duplicate of an existing finding.", Actor, Now);
        Assert.Equal(ExceptionStatus.Cancelled, e.Status);
        Assert.Throws<InvalidStateTransitionException>(() => e.Cancel("Cannot cancel a cancelled exception again.", Actor, Now));
    }

    [Fact]
    public void Approve_from_wrong_state_is_invalid()
        => Assert.Throws<InvalidStateTransitionException>(() => New().ApproveMap(Actor, Now));
}
