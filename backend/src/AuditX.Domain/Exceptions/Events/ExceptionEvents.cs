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

public sealed record ManagementResponseRecordedEvent(Guid ExceptionId, ManagementResponseDecision Decision, Guid ActorUserId) : ExceptionEvent;

public sealed record FindingVerifiedEvent(Guid ExceptionId, Guid VerificationId, VerificationResult Result, Guid VerifiedByUserId) : ExceptionEvent;

public sealed record ExceptionReopenedEvent(Guid ExceptionId, Guid ActorUserId, string Reason, int ReopenCount) : ExceptionEvent;
