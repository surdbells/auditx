namespace AuditX.Application.Exceptions.Dtos;

public sealed record MapActionDto(
    Guid Id, Guid ExceptionId, string Description, Guid OwnerUserId, DateOnly TargetDate,
    string? ExpectedEvidenceType, string Status, DateTimeOffset? CompletedAt, Guid? CompletedBy);

/// <summary>A post-closure follow-up verification of a finding's remediation (P2-B).</summary>
public sealed record FindingVerificationDto(
    Guid Id, Guid ExceptionId, string Result, Guid VerifiedByUserId, DateTimeOffset VerifiedAt, string? Notes);

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
    string? RootCauseCategory,
    Guid OwnerUserId,
    Guid RaisedByUserId,
    DateTimeOffset RaisedAt,
    DateOnly TargetDate,
    decimal? FinancialImpact,
    string? FinancialImpactCurrency,
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
    string? ManagementResponseDecision,
    string? ManagementResponseComment,
    Guid? ManagementRespondedBy,
    DateTimeOffset? ManagementRespondedAt,
    int ReopenCount,
    Guid? ReopenedBy,
    DateTimeOffset? ReopenedAt,
    string? ReopenReason,
    string Version,
    IReadOnlyList<MapActionDto> MapActions,
    IReadOnlyList<FindingVerificationDto> Verifications);

public sealed record ExceptionListItemDto(
    Guid Id, Guid AuditId, string Title, string Severity, string Status, Guid OwnerUserId,
    DateOnly TargetDate, bool IsOverdue, int DaysPastTarget, bool IsRecurrence, DateTimeOffset RaisedAt);

/// <summary>Result of a maker-checker-gateable action: either the updated exception or a captured pending-action id.</summary>
public sealed record ExceptionActionResult(ExceptionDto? Exception, Guid? PendingActionId);

public sealed record ExceptionHistoryEntryDto(Guid Id, string EventType, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? PayloadJson);
