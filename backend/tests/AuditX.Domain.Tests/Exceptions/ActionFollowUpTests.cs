using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Exceptions.Events;

namespace AuditX.Domain.Tests.Exceptions;

/// <summary>Action follow-up: due-date extensions, reminders through an action's life, and reopen on a failed follow-up.</summary>
public sealed class ActionFollowUpTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2027, 1, 1);
    private static readonly DateOnly Target = new(2027, 3, 1);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid ActionOwner = Guid.NewGuid();
    private static readonly Guid Raiser = Guid.NewGuid();
    private static readonly Guid Auditor = Guid.NewGuid();

    private static AuditException New() => AuditException.Raise(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Cash control gap", ExceptionSeverity.Medium, "no segregation",
        "add maker-checker", "controls", "process_gap", Owner, Raiser, Target, false, null, false, null, "{}", Now);

    private static AuditException Approved(DateOnly actionDue)
    {
        var e = New();
        e.AddMapAction("Implement maker-checker", ActionOwner, actionDue, "screenshot");
        e.SubmitMap(Owner, Now);
        e.ApproveMap(Auditor, Now);
        e.ClearDomainEvents();
        return e;
    }

    private static AuditException Closed()
    {
        var e = Approved(Target.AddDays(-5));
        e.MarkMapActionComplete(e.MapActions[0].Id, true, true, ActionOwner, Now);
        e.MarkMapComplete(Owner, Now);
        e.Close("verified", Auditor, Now);
        e.ClearDomainEvents();
        return e;
    }

    // ---- Extensions ----

    [Fact]
    public void An_action_extension_is_requested_then_approved_and_moves_the_date()
    {
        var e = Approved(Target.AddDays(-10));
        var action = e.MapActions[0];

        var request = e.RequestExtension(action.Id, Target.AddDays(-2), "Vendor patch slipped to the next release window.", ActionOwner, Today, Now);
        Assert.Equal(ExtensionRequestStatus.Pending, request.Status);
        Assert.Equal(Target.AddDays(-10), request.CurrentDate);
        var asked = Assert.Single(e.DomainEvents.OfType<DueDateExtensionRequestedEvent>());
        Assert.Equal(Raiser, asked.RaisedByUserId);

        e.ApproveExtension(request.Id, Auditor, null, Now);
        Assert.Equal(ExtensionRequestStatus.Approved, request.Status);
        Assert.Equal(Target.AddDays(-2), action.TargetDate);
        Assert.Equal(Target, e.TargetDate); // still inside the finding's own date
        Assert.Contains(e.DomainEvents, x => x is DueDateExtensionApprovedEvent ev && ev.RequestedBy == ActionOwner);
    }

    [Fact]
    public void Extending_an_action_past_the_finding_date_moves_the_finding_too()
    {
        var e = Approved(Target.AddDays(-1));
        var request = e.RequestExtension(e.MapActions[0].Id, Target.AddDays(20), "The system upgrade moved out by three weeks.", ActionOwner, Today, Now);

        e.ApproveExtension(request.Id, Auditor, "Agreed given the upgrade plan.", Now);

        Assert.Equal(Target.AddDays(20), e.MapActions[0].TargetDate);
        Assert.Equal(Target.AddDays(20), e.TargetDate);
    }

    [Fact]
    public void Nobody_decides_their_own_request()
    {
        var e = Approved(Target.AddDays(-10));
        var request = e.RequestExtension(null, Target.AddDays(30), "The whole remediation depends on a budget approval.", Owner, Today, Now);

        var refused = Assert.Throws<DomainException>(() => e.ApproveExtension(request.Id, Owner, null, Now));
        Assert.Equal("exception.extension_self_decision", refused.Code);
        Assert.Equal(Target, e.TargetDate);
    }

    [Fact]
    public void A_request_must_be_later_reasoned_and_alone()
    {
        var e = Approved(Target.AddDays(-10));
        var id = e.MapActions[0].Id;
        const string Reason = "Waiting on the vendor's fixed release.";

        Assert.Equal("exception.extension_not_later",
            Assert.Throws<DomainException>(() => e.RequestExtension(id, Target.AddDays(-10), Reason, ActionOwner, Today, Now)).Code);
        Assert.Equal("exception.extension_reason_required",
            Assert.Throws<DomainException>(() => e.RequestExtension(id, Target.AddDays(-5), "later", ActionOwner, Today, Now)).Code);

        e.RequestExtension(id, Target.AddDays(-5), Reason, ActionOwner, Today, Now);
        Assert.Equal("exception.extension_already_pending",
            Assert.Throws<DomainException>(() => e.RequestExtension(id, Target.AddDays(-3), Reason, ActionOwner, Today, Now)).Code);

        // A date already in the past cannot be the new date.
        var late = Approved(Today.AddDays(-20));
        Assert.Equal("exception.extension_not_future",
            Assert.Throws<DomainException>(() => late.RequestExtension(late.MapActions[0].Id, Today, Reason, ActionOwner, Today, Now)).Code);
    }

    [Fact]
    public void A_rejection_keeps_the_date_and_needs_a_reason()
    {
        var e = Approved(Target.AddDays(-10));
        var request = e.RequestExtension(null, Target.AddDays(30), "The whole remediation depends on a budget approval.", Owner, Today, Now);

        Assert.Throws<DomainException>(() => e.RejectExtension(request.Id, Auditor, "no", Now));
        e.RejectExtension(request.Id, Auditor, "The risk is too high to wait; use the interim control.", Now);

        Assert.Equal(ExtensionRequestStatus.Rejected, request.Status);
        Assert.Equal(Target, e.TargetDate);
        Assert.Contains(e.DomainEvents, x => x is DueDateExtensionRejectedEvent ev && ev.CurrentDate == Target);
        Assert.Throws<InvalidStateTransitionException>(() => e.ApproveExtension(request.Id, Auditor, null, Now));
    }

    [Fact]
    public void Only_the_requester_withdraws_and_completing_the_plan_withdraws_what_is_left()
    {
        var e = Approved(Target.AddDays(-10));
        var request = e.RequestExtension(null, Target.AddDays(30), "The whole remediation depends on a budget approval.", Owner, Today, Now);

        Assert.Throws<DomainException>(() => e.WithdrawExtension(request.Id, Auditor, Now));
        e.WithdrawExtension(request.Id, Owner, Now);
        Assert.Equal(ExtensionRequestStatus.Withdrawn, request.Status);

        var again = e.RequestExtension(e.MapActions[0].Id, Target.AddDays(-1), "Still waiting on the vendor's release.", ActionOwner, Today, Now);
        e.MarkMapActionComplete(e.MapActions[0].Id, true, true, ActionOwner, Now);
        e.MarkMapComplete(Owner, Now);
        Assert.Equal(ExtensionRequestStatus.Withdrawn, again.Status);
    }

    [Fact]
    public void A_finding_extension_restarts_the_overdue_escalation()
    {
        var e = New();
        e.EscalateMapOverdue(Now, 3);
        Assert.NotNull(e.MapOverdueEscalatedAt);

        var request = e.RequestExtension(null, Target.AddDays(30), "The whole remediation depends on a budget approval.", Owner, Today, Now);
        e.ApproveExtension(request.Id, Auditor, null, Now);

        Assert.Null(e.MapOverdueEscalatedAt);
    }

    // ---- Reminders ----

    [Fact]
    public void Reminders_go_once_per_stage_through_the_action_life()
    {
        var e = Approved(Today.AddDays(5));
        var action = e.MapActions[0];

        // Not yet in the window.
        Assert.Equal(0, e.SendActionReminders(Today.AddDays(-10), Now, 7, 14));

        Assert.Equal(1, e.SendActionReminders(Today, Now, 7, 14));
        Assert.Equal(MapActionReminderStage.DueSoon, action.ReminderStage);
        Assert.Equal(0, e.SendActionReminders(Today.AddDays(1), Now, 7, 14)); // already reminded

        Assert.Equal(1, e.SendActionReminders(Today.AddDays(6), Now, 7, 14));
        Assert.Equal(MapActionReminderStage.Overdue, action.ReminderStage);
        Assert.Equal(0, e.SendActionReminders(Today.AddDays(7), Now, 7, 14));

        Assert.Equal(1, e.SendActionReminders(Today.AddDays(19), Now, 7, 14));
        Assert.Equal(MapActionReminderStage.Escalated, action.ReminderStage);
        Assert.Equal(0, e.SendActionReminders(Today.AddDays(40), Now, 7, 14));

        Assert.Single(e.DomainEvents.OfType<MapActionDueSoonEvent>());
        Assert.Single(e.DomainEvents.OfType<MapActionOverdueEvent>(), ev => ev.OwnerUserId == ActionOwner && ev.DaysOverdue == 1);
        Assert.Single(e.DomainEvents.OfType<MapActionEscalatedEvent>(), ev => ev.DaysOverdue == 14);
    }

    [Fact]
    public void A_long_overdue_action_escalates_straight_away_and_an_extension_starts_again()
    {
        var e = Approved(Today.AddDays(-30));
        var action = e.MapActions[0];

        Assert.Equal(1, e.SendActionReminders(Today, Now, 7, 14));
        Assert.Equal(MapActionReminderStage.Escalated, action.ReminderStage);
        Assert.Empty(e.DomainEvents.OfType<MapActionOverdueEvent>());

        var request = e.RequestExtension(action.Id, Today.AddDays(3), "The fix is built and only needs the release.", ActionOwner, Today, Now);
        e.ApproveExtension(request.Id, Auditor, null, Now);
        Assert.Equal(MapActionReminderStage.None, action.ReminderStage);

        Assert.Equal(1, e.SendActionReminders(Today, Now, 7, 14));
        Assert.Equal(MapActionReminderStage.DueSoon, action.ReminderStage);
    }

    [Fact]
    public void Reminders_skip_completed_actions_and_plans_not_yet_agreed()
    {
        var submitted = New();
        submitted.AddMapAction("Fix", ActionOwner, Today.AddDays(1), null);
        submitted.SubmitMap(Owner, Now);
        Assert.Equal(0, submitted.SendActionReminders(Today, Now, 7, 14));

        var e = Approved(Today.AddDays(1));
        e.MarkMapActionComplete(e.MapActions[0].Id, true, true, ActionOwner, Now);
        Assert.Equal(0, e.SendActionReminders(Today, Now, 7, 14));
    }

    // ---- Failed verification reopens ----

    [Fact]
    public void A_failed_verification_reopens_with_a_new_date_and_supersedes_the_old_plan()
    {
        var e = Closed();
        var oldAction = e.MapActions[0];

        Assert.Equal("exception.failed_verification_notes_required",
            Assert.Throws<DomainException>(() => e.AddVerification(VerificationResult.Failed, "bad", Auditor, Now, Today, Target.AddDays(60))).Code);
        Assert.Equal("exception.reopen_target_date_required",
            Assert.Throws<DomainException>(() => e.AddVerification(
                VerificationResult.Failed, "The control was bypassed twice in the sample.", Auditor, Now, Today)).Code);
        Assert.Equal("exception.reopen_target_date_past",
            Assert.Throws<DomainException>(() => e.AddVerification(
                VerificationResult.Failed, "The control was bypassed twice in the sample.", Auditor, Now, Today, Today)).Code);
        Assert.Empty(e.Verifications);

        e.AddVerification(VerificationResult.Failed, "The control was bypassed twice in the sample.", Auditor, Now, Today, Target.AddDays(60));

        Assert.Equal(ExceptionStatus.Open, e.Status);
        Assert.Equal(1, e.ReopenCount);
        Assert.Equal(Target.AddDays(60), e.TargetDate);
        Assert.StartsWith("Follow-up verification failed:", e.ReopenReason);
        Assert.Equal(MapActionStatus.Superseded, oldAction.Status);
        Assert.Empty(e.CurrentMapActions);
        var reopened = Assert.Single(e.DomainEvents.OfType<ExceptionReopenedEvent>());
        Assert.Equal(Owner, reopened.OwnerUserId);

        // The new plan is judged on its own actions, not the superseded ones.
        Assert.Throws<DomainException>(() => e.SubmitMap(Owner, Now));
        e.AddMapAction("Enforce maker-checker in the core system", ActionOwner, Target.AddDays(30), null);
        e.SubmitMap(Owner, Now);
        e.ApproveMap(Auditor, Now);
        Assert.Throws<DomainException>(() => e.MarkMapComplete(Owner, Now));
        Assert.Equal(2, e.MapActions.Count); // history kept
    }

    [Fact]
    public void A_passed_verification_leaves_the_finding_closed()
    {
        var e = Closed();
        e.AddVerification(VerificationResult.Passed, null, Auditor, Now, Today);
        Assert.Equal(ExceptionStatus.Closed, e.Status);
        Assert.Equal(0, e.ReopenCount);
    }
}
