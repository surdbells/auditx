namespace AuditX.Application.Ac.Dtos;

// ---- AC packs ----

/// <summary>The 202 payload from POST generate: the pack id IS the job handle.</summary>
public sealed record AcPackGenerationAcceptedDto(Guid AcPackId, string Status);

/// <summary>One produced artefact descriptor surfaced on the pack metadata.</summary>
public sealed record AcProducedArtefactDto(string Format, string ContentType, long SizeBytes, string Sha256);

/// <summary>AC-pack metadata / status surface.</summary>
public sealed record AcPackDto(
    Guid Id,
    int VersionNumber,
    string Status,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? AcMeetingLabel,
    string? Sha256Hash,
    string? CiaSupplementaryText,
    IReadOnlyList<string> RequestedFormats,
    IReadOnlyList<AcProducedArtefactDto> ProducedArtefacts,
    string? FailureReason,
    Guid GeneratedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string Version);

/// <summary>Compact list item for the pack list.</summary>
public sealed record AcPackListItemDto(
    Guid Id,
    int VersionNumber,
    string Status,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? AcMeetingLabel,
    string? Sha256Hash,
    IReadOnlyList<string> ProducedFormats,
    Guid GeneratedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);

/// <summary>A single AC-pack distribution-log row.</summary>
public sealed record AcPackDistributionDto(
    Guid Id,
    Guid AcPackId,
    int AcPackVersionNumber,
    Guid RecipientUserId,
    DateTimeOffset DispatchedAt,
    Guid DispatchedBy,
    string Outcome);

/// <summary>The outcome of a distribute request: how many AC recipients were dispatched to.</summary>
public sealed record AcPackDistributionResultDto(int RecipientCount);

/// <summary>The downloaded artefact bytes returned to the controller for streaming.</summary>
public sealed record AcPackArtefactResult(byte[] Content, string ContentType, string Filename, string Sha256Hash);

// ---- AC pack analytics (from the immutable snapshot, restricted-filter applied per requester) ----

/// <summary>A material finding as shown to the requester. <see cref="Restricted"/> hides detail (title nulled) when the caller is not allow-listed.</summary>
public sealed record AcMaterialFindingDto(
    Guid ExceptionId,
    Guid AuditId,
    string? Title,
    string Severity,
    string Status,
    Guid? AuditableEntityId,
    DateTimeOffset RaisedAt,
    DateOnly TargetDate,
    bool Restricted);

public sealed record AcSeverityCountDto(string Severity, int Count);

public sealed record AcSanctionsConsistencyRowDto(
    string BusinessUnit,
    int CaseCount,
    int WithinGridCount,
    decimal GridAdherencePercent,
    int DeviationCount,
    int AppealCount,
    decimal AppealRatePercent);

public sealed record AcRecurrenceClusterDto(
    Guid Id,
    Guid AuditableEntityId,
    string? Category,
    int ClosedExceptionCount,
    int WindowMonths,
    DateTimeOffset FirstOccurredAt,
    DateTimeOffset LastOccurredAt);

/// <summary>
/// The AC pack's analytics surface (M13). Aggregates are coherent regardless of restriction; detail is hidden
/// per-requester via <see cref="AcMaterialFindingDto.Restricted"/>. Sanctions are aggregate-only (no subject id).
/// </summary>
public sealed record AcPackAnalyticsDto(
    int VersionNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int TotalPlans,
    int PlanItemsTotal,
    int PlanItemsCompleted,
    decimal PlanCompletionPercent,
    int OpenExceptionTotal,
    double? AverageClosureDays,
    IReadOnlyList<AcSeverityCountDto> ExceptionsBySeverity,
    IReadOnlyList<AcMaterialFindingDto> MaterialFindings,
    int SanctionsTotalCases,
    decimal SanctionsGridAdherencePercent,
    decimal SanctionsAppealRatePercent,
    IReadOnlyList<AcSanctionsConsistencyRowDto> SanctionsByBusinessUnit,
    IReadOnlyList<AcRecurrenceClusterDto> RecurrenceClusters,
    DateTimeOffset GeneratedAtUtc);

// ---- AC dashboard (live aggregates, read-only) ----

/// <summary>
/// The read-only AC dashboard (M13). Live aggregates from M9 analytics; sanctions are aggregate-only (no subject
/// id anywhere). Material-finding detail is hidden per-requester for restricted findings; counts stay coherent.
/// </summary>
public sealed record AcDashboardDto(
    int TotalPlans,
    int PlanItemsTotal,
    int PlanItemsCompleted,
    decimal PlanCompletionPercent,
    int OpenExceptionTotal,
    double? AverageClosureDays,
    IReadOnlyList<AcSeverityCountDto> ExceptionsBySeverity,
    IReadOnlyList<AcMaterialFindingDto> MaterialFindings,
    int SanctionsTotalCases,
    decimal SanctionsGridAdherencePercent,
    decimal SanctionsAppealRatePercent,
    IReadOnlyList<AcSanctionsConsistencyRowDto> SanctionsByBusinessUnit,
    IReadOnlyList<AcRecurrenceClusterDto> RecurrenceClusters);

// ---- AC action items ----

public sealed record AcActionItemDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    Guid? AssignedToUserId,
    DateOnly? DueDate,
    string? ClosureResponse,
    Guid CreatedByUserId,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByUserId,
    DateTimeOffset? AcknowledgedAt,
    Guid? AcknowledgedByUserId,
    string Version);

// ---- AC comments ----

public sealed record AcCommentDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string CommentText,
    Guid AuthorUserId,
    DateTimeOffset CommentedAt);

// ---- Finding visibility restriction ----

public sealed record FindingVisibilityRestrictionDto(
    Guid Id,
    string FindingType,
    Guid FindingId,
    IReadOnlyList<Guid> AllowedUserIds,
    string? Reason,
    Guid RestrictedByUserId,
    DateTimeOffset RestrictedAt);
