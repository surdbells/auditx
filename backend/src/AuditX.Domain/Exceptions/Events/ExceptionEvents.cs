using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Exceptions.Events;

public abstract record ExceptionEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ExceptionRaisedEvent(Guid ExceptionId, Guid AuditId, Guid ChecklistItemId, ExceptionSeverity Severity, Guid OwnerUserId, bool IsRecurrence) : ExceptionEvent;

public sealed record ExceptionSeverityChangedEvent(Guid ExceptionId, ExceptionSeverity From, ExceptionSeverity To, Guid ActorUserId, string Reason) : ExceptionEvent;

public sealed record ExceptionOwnerReassignedEvent(Guid ExceptionId, Guid OldOwnerUserId, Guid NewOwnerUserId) : ExceptionEvent;

public sealed record MapSubmittedEvent(Guid ExceptionId, Guid SubmittedBy) : ExceptionEvent;

/// <summary>An overdue MAP was escalated up the reporting line. Notifies the owner's line manager (payload OwnerUserId).</summary>
public sealed record MapOverdueEscalatedEvent(Guid ExceptionId, Guid OwnerUserId, ExceptionSeverity Severity, DateOnly TargetDate, int DaysOverdue) : ExceptionEvent;

public sealed record MapApprovedEvent(Guid ExceptionId, Guid ApprovedBy) : ExceptionEvent;

public sealed record MapRejectedEvent(Guid ExceptionId, Guid RejectedBy, string Reason) : ExceptionEvent;

public sealed record MapReturnedForEvidenceEvent(Guid ExceptionId, Guid ActorUserId, string Reason) : ExceptionEvent;

public sealed record MapActionCompletedEvent(Guid ExceptionId, Guid ActionId, Guid CompletedBy) : ExceptionEvent;

public sealed record MapCompletedEvent(Guid ExceptionId, Guid ActorUserId) : ExceptionEvent;

public sealed record ExceptionPendingCiaEvent(Guid ExceptionId, Guid ClosedBy) : ExceptionEvent;

public sealed record ExceptionClosedEvent(Guid ExceptionId, Guid ClosedBy, Guid? CiaCountersignedBy) : ExceptionEvent;

public sealed record ExceptionCancelledEvent(Guid ExceptionId, Guid ActorUserId, string Reason) : ExceptionEvent;

/// <summary>
/// Management's response to a finding was recorded. <c>RaisedByUserId</c> (the auditor who raised the finding) is the
/// default notification recipient; <c>OnBehalfOfManagement</c> is true when the audit team recorded it for management.
/// </summary>
public sealed record ManagementResponseRecordedEvent(
    Guid ExceptionId, Guid AuditId, ManagementResponseDecision Decision, Guid ActorUserId, Guid RaisedByUserId, bool OnBehalfOfManagement) : ExceptionEvent;

public sealed record FindingVerifiedEvent(Guid ExceptionId, Guid VerificationId, VerificationResult Result, Guid VerifiedByUserId) : ExceptionEvent;

/// <summary>A closed finding was reopened. Its owner (<c>OwnerUserId</c>) is told it needs a new plan by <c>TargetDate</c>.</summary>
public sealed record ExceptionReopenedEvent(
    Guid ExceptionId, Guid ActorUserId, string Reason, int ReopenCount, Guid OwnerUserId, string Title, DateOnly TargetDate) : ExceptionEvent;

/// <summary>
/// Someone remediating a finding asked for more time. <c>RaisedByUserId</c> (the auditor who raised the finding) is told,
/// so the audit side can decide. <c>ActionId</c> is null when the finding's own target date is to move.
/// </summary>
public sealed record DueDateExtensionRequestedEvent(
    Guid ExceptionId, Guid AuditId, string Title, Guid RequestId, Guid? ActionId, Guid RequestedBy, Guid RaisedByUserId,
    DateOnly CurrentDate, DateOnly RequestedDate) : ExceptionEvent;

/// <summary>An extension request was approved and the date moved. The person who asked (<c>RequestedBy</c>) is told.</summary>
public sealed record DueDateExtensionApprovedEvent(
    Guid ExceptionId, string Title, Guid RequestId, Guid? ActionId, Guid RequestedBy, Guid DecidedBy, DateOnly RequestedDate, string? Note)
    : ExceptionEvent;

/// <summary>An extension request was rejected; the date stays. The person who asked (<c>RequestedBy</c>) is told why.</summary>
public sealed record DueDateExtensionRejectedEvent(
    Guid ExceptionId, string Title, Guid RequestId, Guid? ActionId, Guid RequestedBy, Guid DecidedBy, DateOnly CurrentDate, string? Note)
    : ExceptionEvent;

/// <summary>A remediation action falls due within the reminder window. Its owner (<c>OwnerUserId</c>) is reminded.</summary>
public sealed record MapActionDueSoonEvent(
    Guid ExceptionId, string Title, Guid ActionId, string Description, Guid OwnerUserId, DateOnly TargetDate, int DaysLeft) : ExceptionEvent;

/// <summary>A remediation action passed its due date. Its owner (<c>OwnerUserId</c>) is told.</summary>
public sealed record MapActionOverdueEvent(
    Guid ExceptionId, string Title, Guid ActionId, string Description, Guid OwnerUserId, DateOnly TargetDate, int DaysOverdue) : ExceptionEvent;

/// <summary>A remediation action stayed overdue past the escalation threshold. The owner's line manager is told.</summary>
public sealed record MapActionEscalatedEvent(
    Guid ExceptionId, string Title, Guid ActionId, string Description, Guid OwnerUserId, DateOnly TargetDate, int DaysOverdue) : ExceptionEvent;
