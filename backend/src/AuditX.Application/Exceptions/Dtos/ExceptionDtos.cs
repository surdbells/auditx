namespace AuditX.Application.Exceptions.Dtos;

public sealed record MapActionDto(
    Guid Id, Guid ExceptionId, string Description, Guid OwnerUserId, DateOnly TargetDate,
    string? ExpectedEvidenceType, string Status, DateTimeOffset? CompletedAt, Guid? CompletedBy);

public sealed record ExceptionDto(
    Guid Id,
    Guid AuditId,
    Guid ChecklistItemId,
    Guid? AuditableEntityId,
    string Title,
    string Severity,
    string Status,
    string? RootCause,
    string? Recommendation,
    string? Category,
    Guid OwnerUserId,
    Guid RaisedByUserId,
    DateTimeOffset RaisedAt,
    DateOnly TargetDate,
    bool TargetDateOverridden,
    bool IsRecurrence,
    Guid? RecurrenceOfExceptionId,
    bool CiaPending,
    bool IsOverdue,
    int DaysPastTarget,
    DateTimeOffset? MapSubmittedAt,
    DateTimeOffset? MapApprovedAt,
    string? MapRejectionReason,
    string? ClosureEvidenceNote,
    Guid? ClosedBy,
    DateTimeOffset? ClosedAt,
    Guid? CiaCountersignedBy,
    string? CancellationReason,
    string Version,
    IReadOnlyList<MapActionDto> MapActions);

public sealed record ExceptionListItemDto(
    Guid Id, Guid AuditId, string Title, string Severity, string Status, Guid OwnerUserId,
    DateOnly TargetDate, bool IsOverdue, int DaysPastTarget, bool IsRecurrence);

/// <summary>Result of a maker-checker-gateable action: either the updated exception or a captured pending-action id.</summary>
public sealed record ExceptionActionResult(ExceptionDto? Exception, Guid? PendingActionId);

public sealed record ExceptionHistoryEntryDto(Guid Id, string EventType, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? PayloadJson);
