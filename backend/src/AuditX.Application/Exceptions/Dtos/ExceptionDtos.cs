namespace AuditX.Application.Exceptions.Dtos;

public sealed record MapActionDto(
    Guid Id, Guid ExceptionId, string Description, Guid OwnerUserId, DateOnly TargetDate,
    string? ExpectedEvidenceType, string Status, DateTimeOffset? CompletedAt, Guid? CompletedBy, string ReminderStage);

/// <summary>A request to move the finding's target date (<c>ActionId</c> null) or one action's due date.</summary>
public sealed record DueDateExtensionRequestDto(
    Guid Id, Guid? ActionId, DateOnly CurrentDate, DateOnly RequestedDate, string Reason, Guid RequestedBy,
    DateTimeOffset RequestedAt, string Status, Guid? DecidedBy, DateTimeOffset? DecidedAt, string? DecisionNote);

/// <summary>
/// One thing waiting on the signed-in user in the follow-up of findings. <c>Kind</c>: <c>respond</c> (record
/// management's response), <c>submit_plan</c> (write or rewrite the action plan), <c>complete_action</c> (finish one
/// action and attach its evidence), <c>complete_plan</c> (every action is done: mark the plan complete) or
/// <c>decide_extension</c> (someone asked for more time on a finding you raised).
/// </summary>
public sealed record MyActionItemDto(
    string Kind, Guid ExceptionId, string ExceptionTitle, string Severity, string ExceptionStatus, Guid AuditId,
    Guid? ActionId, Guid? ExtensionRequestId, string? Description, DateOnly? DueDate, int? DaysUntilDue,
    bool IsOverdue, bool ExtensionPending);

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
    string? NonConformanceCategory,
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
    bool ManagementResponseOnBehalf,
    DateOnly? ManagementResponseDueDate,
    string? ManagementResponseTimeliness,
    int ReopenCount,
    Guid? ReopenedBy,
    DateTimeOffset? ReopenedAt,
    string? ReopenReason,
    string Version,
    IReadOnlyList<MapActionDto> MapActions,
    IReadOnlyList<FindingVerificationDto> Verifications,
    IReadOnlyList<DueDateExtensionRequestDto> ExtensionRequests);

public sealed record ExceptionListItemDto(
    Guid Id, Guid AuditId, string Title, string Severity, string Status, Guid OwnerUserId,
    DateOnly TargetDate, bool IsOverdue, int DaysPastTarget, bool IsRecurrence, DateTimeOffset RaisedAt);

/// <summary>Result of a maker-checker-gateable action: either the updated exception or a captured pending-action id.</summary>
public sealed record ExceptionActionResult(ExceptionDto? Exception, Guid? PendingActionId);

public sealed record ExceptionHistoryEntryDto(Guid Id, string EventType, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? PayloadJson);

public sealed record ExceptionRaisingRuleDto(Guid Id, string ResponseType, bool AllowOnNa, decimal? ScoreThreshold, bool IsActive);

/// <summary>An exception (finding) linked to a root-cause gap, with light display fields.</summary>
public sealed record RootCauseGapLinkedExceptionDto(Guid LinkId, Guid ExceptionId, string Title, string Severity, string Status);

/// <summary>One corrective action in a root-cause gap's remediation plan.</summary>
public sealed record RootCauseGapRemediationDto(
    Guid Id, string Description, Guid OwnerUserId, DateOnly? DueDate, string Status,
    string? CompletionNote, Guid? CompletedByUserId, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);

public sealed record RootCauseGapDto(
    Guid Id, string Title, string? Description, string? Category, Guid OwnerUserId, DateOnly? TargetDate, string Status,
    Guid IdentifiedByUserId, DateTimeOffset IdentifiedAt, string? ClosureRationale, Guid? ClosedByUserId, DateTimeOffset? ClosedAt,
    string Version, IReadOnlyList<RootCauseGapLinkedExceptionDto> LinkedExceptions,
    IReadOnlyList<RootCauseGapRemediationDto> Remediations);

public sealed record RootCauseGapListItemDto(
    Guid Id, string Title, string? Category, Guid OwnerUserId, DateOnly? TargetDate, string Status, DateTimeOffset IdentifiedAt, int LinkedExceptionCount);
