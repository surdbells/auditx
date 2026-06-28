namespace AuditX.Application.Ac.Generation;

/// <summary>One material finding (Critical/High open exception) projected into the AC pack. Sourced from M9 analytics.</summary>
public sealed record AcMaterialFindingLine(
    Guid ExceptionId,
    Guid AuditId,
    string Title,
    string Severity,
    string Status,
    Guid? AuditableEntityId,
    DateTimeOffset RaisedAt,
    DateOnly TargetDate);

/// <summary>One open-exception count for a severity tier (exception portfolio rollup).</summary>
public sealed record AcSeverityCountLine(string Severity, int Count);

/// <summary>One sanctions-consistency row by business unit. AGGREGATE ONLY — subject identity is physically absent.</summary>
public sealed record AcSanctionsConsistencyLine(
    string BusinessUnit,
    int CaseCount,
    int WithinGridCount,
    decimal GridAdherencePercent,
    int DeviationCount,
    int AppealCount,
    decimal AppealRatePercent);

/// <summary>One detected recurrence cluster projected into the AC pack.</summary>
public sealed record AcRecurrenceClusterLine(
    Guid Id,
    Guid AuditableEntityId,
    string? Category,
    int ClosedExceptionCount,
    int WindowMonths,
    DateTimeOffset FirstOccurredAt,
    DateTimeOffset LastOccurredAt);

/// <summary>
/// The pure, immutable data an AC pack is assembled from (M13). Sourced entirely from the M9
/// <see cref="AuditX.Application.Abstractions.Analytics.IAnalyticsQueryService"/> port (material findings, plan
/// status, exception portfolio, sanctions consistency [AGGREGATE — no subject id], recurrence clusters). Serialized
/// into <c>AcPack.ContentSnapshotJson</c> at completion; re-render reads the snapshot, never re-queries live.
///
/// The restricted-finding allow-list is NOT applied here (the snapshot holds the FULL list); it is applied
/// per-requester at read so restricted items still count toward aggregate totals (detail hidden per caller).
/// </summary>
public sealed record AcPackComposition(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? AcMeetingLabel,
    int VersionNumber,
    // Plan status (M9 G5)
    int TotalPlans,
    int PlanItemsTotal,
    int PlanItemsCompleted,
    decimal PlanCompletionPercent,
    // Exception portfolio (M9 G2)
    int OpenExceptionTotal,
    double? AverageClosureDays,
    IReadOnlyList<AcSeverityCountLine> ExceptionsBySeverity,
    // Material findings (M9 — AC widget)
    IReadOnlyList<AcMaterialFindingLine> MaterialFindings,
    // Sanctions consistency (M9 G3 — aggregate only)
    int SanctionsTotalCases,
    decimal SanctionsGridAdherencePercent,
    decimal SanctionsAppealRatePercent,
    IReadOnlyList<AcSanctionsConsistencyLine> SanctionsByBusinessUnit,
    // Recurrence clusters (M9 G6)
    IReadOnlyList<AcRecurrenceClusterLine> RecurrenceClusters,
    DateTimeOffset GeneratedAtUtc);
