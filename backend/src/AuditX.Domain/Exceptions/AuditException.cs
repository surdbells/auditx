using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions.Events;

namespace AuditX.Domain.Exceptions;

/// <summary>
/// An audit exception / finding (M6). Named <c>AuditException</c> to avoid clashing with
/// <see cref="System.Exception"/>. Aggregate root over its Management Action Plan (MAP) actions. Drives the
/// 7-state lifecycle Open → MAP Submitted → MAP Approved → Pending Closure → Closed, with reject/cancel/return
/// edges. The Critical-closure CIA hold is the <see cref="CiaPending"/> flag inside Pending Closure, not a
/// separate status. Segregation-of-duties on closure is enforced in the application layer (it maps to 403).
/// </summary>
public sealed class AuditException : AggregateRoot
{
    private readonly List<MapAction> _mapActions = [];

    private AuditException()
    {
    }

    public Guid AuditId { get; private set; }

    public Guid ChecklistItemId { get; private set; }

    public Guid? AuditableEntityId { get; private set; }

    public string Title { get; private set; } = null!;

    public ExceptionSeverity Severity { get; private set; }

    public string RootCause { get; private set; } = null!;

    public string Recommendation { get; private set; } = null!;

    public string? Category { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public Guid RaisedByUserId { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public DateOnly TargetDate { get; private set; }

    public bool TargetDateOverridden { get; private set; }

    public string? TargetDateOverrideRationale { get; private set; }

    public ExceptionStatus Status { get; private set; }

    public bool IsRecurrence { get; private set; }

    public Guid? RecurrenceOfExceptionId { get; private set; }

    public bool CiaPending { get; private set; }

    public string ConfigurationVersionsJson { get; private set; } = "{}";

    public string? ClosureEvidenceNote { get; private set; }

    public Guid? ClosedBy { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? CiaCountersignedBy { get; private set; }

    public DateTimeOffset? CiaCountersignedAt { get; private set; }

    public string? MapRejectionReason { get; private set; }

    public DateTimeOffset? MapSubmittedAt { get; private set; }

    public Guid? MapSubmittedBy { get; private set; }

    public DateTimeOffset? MapApprovedAt { get; private set; }

    public Guid? MapApprovedBy { get; private set; }

    public string? CancellationReason { get; private set; }

    public Guid? CancelledBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<MapAction> MapActions => _mapActions.AsReadOnly();

    public bool RequiresCiaCountersign => Severity == ExceptionSeverity.Critical;

    public static AuditException Raise(
        Guid auditId, Guid checklistItemId, Guid? auditableEntityId, string title, ExceptionSeverity severity,
        string rootCause, string recommendation, string? category, Guid ownerUserId, Guid raisedBy,
        DateOnly targetDate, bool targetDateOverridden, string? overrideRationale,
        bool isRecurrence, Guid? recurrenceOfExceptionId, string? configurationVersionsJson, DateTimeOffset nowUtc)
    {
        var exception = new AuditException
        {
            AuditId = auditId,
            ChecklistItemId = checklistItemId,
            AuditableEntityId = auditableEntityId,
            Title = Guard.NotNullOrWhiteSpace(title, "exception.title_required", "A title is required."),
            Severity = severity,
            RootCause = Guard.NotNullOrWhiteSpace(rootCause, "exception.root_cause_required", "A root cause is required."),
            Recommendation = Guard.NotNullOrWhiteSpace(recommendation, "exception.recommendation_required", "A recommendation is required."),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            OwnerUserId = ownerUserId,
            RaisedByUserId = raisedBy,
            RaisedAt = nowUtc,
            TargetDate = targetDate,
            TargetDateOverridden = targetDateOverridden,
            TargetDateOverrideRationale = string.IsNullOrWhiteSpace(overrideRationale) ? null : overrideRationale.Trim(),
            Status = ExceptionStatus.Open,
            IsRecurrence = isRecurrence,
            RecurrenceOfExceptionId = recurrenceOfExceptionId,
            ConfigurationVersionsJson = string.IsNullOrWhiteSpace(configurationVersionsJson) ? "{}" : configurationVersionsJson,
        };
        exception.RaiseDomainEvent(new ExceptionRaisedEvent(exception.Id, auditId, checklistItemId, severity, ownerUserId, isRecurrence));
        return exception;
    }

    public void ChangeSeverity(ExceptionSeverity newSeverity, string reason, Guid actorUserId)
    {
        EnsureNotTerminal("exception.severity_locked");
        var trimmed = Guard.MinLength(reason, 20, "exception.severity_reason_required", "A reason of at least 20 characters is required.");
        if (newSeverity == Severity)
        {
            return;
        }

        var before = Severity;
        Severity = newSeverity;
        RaiseDomainEvent(new ExceptionSeverityChangedEvent(Id, before, newSeverity, actorUserId, trimmed));
    }

    public void Reassign(Guid newOwnerUserId, Guid actorUserId)
    {
        EnsureNotTerminal("exception.owner_locked");
        if (newOwnerUserId == OwnerUserId)
        {
            return;
        }

        var old = OwnerUserId;
        OwnerUserId = newOwnerUserId;
        RaiseDomainEvent(new ExceptionOwnerReassignedEvent(Id, old, newOwnerUserId));
    }

    public MapAction AddMapAction(string description, Guid ownerUserId, DateOnly targetDate, string? expectedEvidenceType)
    {
        EnsureStatus("exception.map_locked", ExceptionStatus.Open, ExceptionStatus.MapRejected);
        if (targetDate > TargetDate)
        {
            throw new DomainException("exception.action_target_after_exception", "A remediation action cannot be due after the exception's target date.");
        }

        var action = new MapAction(Id, description, ownerUserId, targetDate, expectedEvidenceType);
        _mapActions.Add(action);
        return action;
    }

    public void SubmitMap(Guid actorUserId, DateTimeOffset nowUtc)
    {
        if (_mapActions.Count == 0)
        {
            throw new DomainException("exception.map_requires_action", "A MAP must have at least one remediation action.");
        }

        Transition(ExceptionStatus.MapSubmitted, ExceptionStatus.Open, ExceptionStatus.MapRejected);
        MapSubmittedAt = nowUtc;
        MapSubmittedBy = actorUserId;
        MapRejectionReason = null;
        RaiseDomainEvent(new MapSubmittedEvent(Id, actorUserId));
    }

    public void ApproveMap(Guid actorUserId, DateTimeOffset nowUtc)
    {
        Transition(ExceptionStatus.MapApproved, ExceptionStatus.MapSubmitted);
        MapApprovedAt = nowUtc;
        MapApprovedBy = actorUserId;
        RaiseDomainEvent(new MapApprovedEvent(Id, actorUserId));
    }

    public void RejectMap(string reason, Guid actorUserId)
    {
        var trimmed = Guard.MinLength(reason, 20, "exception.reject_reason_required", "A rejection reason of at least 20 characters is required.");
        Transition(ExceptionStatus.MapRejected, ExceptionStatus.MapSubmitted);
        MapRejectionReason = trimmed;
        RaiseDomainEvent(new MapRejectedEvent(Id, actorUserId, trimmed));
    }

    public void MarkMapActionComplete(Guid actionId, bool requireEvidence, bool hasEvidence, Guid completedBy, DateTimeOffset nowUtc)
    {
        EnsureStatus("exception.map_not_approved", ExceptionStatus.MapApproved);
        var action = _mapActions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new DomainException("exception.action_not_found", "Remediation action not found.");
        if (requireEvidence && !hasEvidence)
        {
            throw new DomainException("exception.evidence_required", "Evidence is required to complete this remediation action.");
        }

        action.MarkComplete(completedBy, nowUtc);
        RaiseDomainEvent(new MapActionCompletedEvent(Id, actionId, completedBy));
    }

    public void MarkMapComplete(Guid actorUserId)
    {
        EnsureStatus("exception.map_not_approved", ExceptionStatus.MapApproved);
        if (_mapActions.Any(a => a.Status != MapActionStatus.Complete))
        {
            throw new DomainException("exception.actions_incomplete", "All remediation actions must be complete first.");
        }

        Transition(ExceptionStatus.PendingClosure, ExceptionStatus.MapApproved);
        RaiseDomainEvent(new MapCompletedEvent(Id, actorUserId));
    }

    public void ReturnForEvidence(string reason, Guid actorUserId)
    {
        var trimmed = Guard.MinLength(reason, 20, "exception.return_reason_required", "A reason of at least 20 characters is required.");
        Transition(ExceptionStatus.MapApproved, ExceptionStatus.PendingClosure);
        RaiseDomainEvent(new MapReturnedForEvidenceEvent(Id, actorUserId, trimmed));
    }

    /// <summary>Close (or place on CIA hold for Critical). SoD and the evidence gate are enforced by the caller.</summary>
    public void Close(string? closureNote, Guid closedBy, DateTimeOffset nowUtc)
    {
        EnsureStatus("exception.not_pending_closure", ExceptionStatus.PendingClosure);
        ClosureEvidenceNote = string.IsNullOrWhiteSpace(closureNote) ? null : closureNote.Trim();
        ClosedBy = closedBy;

        if (RequiresCiaCountersign)
        {
            CiaPending = true;
            RaiseDomainEvent(new ExceptionPendingCiaEvent(Id, closedBy));
            return;
        }

        Status = ExceptionStatus.Closed;
        ClosedAt = nowUtc;
        RaiseDomainEvent(new ExceptionClosedEvent(Id, closedBy, null));
    }

    public void CiaCountersign(Guid ciaUserId, DateTimeOffset nowUtc)
    {
        if (Status != ExceptionStatus.PendingClosure || !CiaPending)
        {
            throw new InvalidStateTransitionException("exception.not_pending_cia", "The exception is not awaiting CIA countersignature.");
        }

        CiaPending = false;
        Status = ExceptionStatus.Closed;
        CiaCountersignedBy = ciaUserId;
        CiaCountersignedAt = nowUtc;
        ClosedAt = nowUtc;
        RaiseDomainEvent(new ExceptionClosedEvent(Id, ClosedBy ?? ciaUserId, ciaUserId));
    }

    public void Cancel(string reason, Guid actorUserId, DateTimeOffset nowUtc)
    {
        if (Status is ExceptionStatus.Closed or ExceptionStatus.Cancelled)
        {
            throw new InvalidStateTransitionException("exception.invalid_transition", "A closed or cancelled exception cannot be cancelled.");
        }

        CancellationReason = Guard.MinLength(reason, 20, "exception.cancel_reason_too_short", "A cancellation reason of at least 20 characters is required.");
        CancelledBy = actorUserId;
        CancelledAt = nowUtc;
        Status = ExceptionStatus.Cancelled;
        RaiseDomainEvent(new ExceptionCancelledEvent(Id, actorUserId, CancellationReason));
    }

    private void Transition(ExceptionStatus to, params ExceptionStatus[] from)
    {
        if (from.Length > 0 && !from.Contains(Status))
        {
            throw new InvalidStateTransitionException("exception.invalid_transition", $"Cannot move to {to} from {Status}.");
        }

        Status = to;
    }

    private void EnsureStatus(string code, params ExceptionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStateTransitionException(code, $"Operation not permitted while the exception is {Status.ToString().ToLowerInvariant()}.");
        }
    }

    private void EnsureNotTerminal(string code)
    {
        if (Status is ExceptionStatus.Closed or ExceptionStatus.Cancelled)
        {
            throw new InvalidStateTransitionException(code, "The exception is closed or cancelled.");
        }
    }
}
