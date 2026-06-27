namespace AuditX.Application.Sanctions.Dtos;

/// <summary>Full case projection (returned to case-team members; subject identity present).</summary>
public sealed record SanctionsCaseDto(
    Guid Id,
    Guid ExceptionId,
    Guid? SubjectUserId,
    bool SubjectMasked,
    string Status,
    string? Category,
    string Severity,
    bool IsRecurrence,
    string? Recommendation,
    int? GridConsultedVersion,
    string? GridRecommendedRange,
    bool WithinGridRange,
    string? DeviationReason,
    string? HrOutcomeJson,
    string? DcDecisionJson,
    Guid TriggeredBy,
    DateTimeOffset TriggeredAt,
    Guid? RecommendedBy,
    DateTimeOffset? RecommendedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? HrOutcomeAt,
    DateTimeOffset? DcDecisionAt,
    Guid? ClosedBy,
    DateTimeOffset? ClosedAt,
    string Version,
    IReadOnlyList<Guid> TeamMemberUserIds,
    Guid? LatestAppealId = null,
    string? LatestAppealStatus = null,
    string? LatestAppealVersion = null);

/// <summary>Masked-capable list projection: the subject is always masked in lists (A1).</summary>
public sealed record SanctionsCaseListDto(
    Guid Id, Guid ExceptionId, Guid? SubjectUserId, bool SubjectMasked, string Status,
    string? Category, string Severity, bool IsRecurrence, DateTimeOffset TriggeredAt);

public sealed record SanctionsGridVersionDto(
    Guid Id, int VersionNumber, string GridDefinitionJson, bool IsActive, string? ActivationReason,
    Guid CreatedByUserId, DateTimeOffset CreatedAtUtc, Guid? ActivatedBy, DateTimeOffset? ActivatedAt, string Version);

public sealed record GridConsultationDto(string? RecommendedRange, bool WithinRange);

public sealed record SanctionsAppealDto(
    Guid Id, Guid SanctionsCaseId, Guid AppellantUserId, Guid RoutedToUserId, string Basis,
    string Status, string? DecisionJson, DateTimeOffset FiledAt, DateTimeOffset? DecidedAt, string Version);

/// <summary>Result of a maker-checker-gateable grid action: either the grid version or a captured pending-action id.</summary>
public sealed record SanctionsGridActionResult(SanctionsGridVersionDto? GridVersion, Guid? PendingActionId);
