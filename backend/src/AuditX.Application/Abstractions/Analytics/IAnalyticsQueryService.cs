namespace AuditX.Application.Abstractions.Analytics;

/// <summary>
/// Read-model port for the M9 KPI projections. Aggregate-only: efficient GroupBy/Count/conditional-sum over the
/// AppDbContext read models, no audit-trail side effects. CRITICAL (FR-M7-010 / NFR-SEC-007): NO projection here
/// selects the sanctions subject identity — sanctions are aggregated by category/business unit, never by subject.
/// </summary>
public interface IAnalyticsQueryService
{
    Task<FunctionPerformanceDto> FunctionPerformanceAsync(CancellationToken cancellationToken = default);

    Task<ExceptionPortfolioDto> ExceptionPortfolioAsync(CancellationToken cancellationToken = default);

    /// <summary>Sanctions consistency aggregated by business unit (category). Physically omits subject_user_id.</summary>
    Task<SanctionsConsistencyDto> SanctionsConsistencyAsync(CancellationToken cancellationToken = default);

    /// <summary>Per-audit-lead performance scorecards. The caller filters self-coverage in the handler.</summary>
    Task<IReadOnlyList<PerformanceScorecardDto>> PerformanceScorecardsAsync(CancellationToken cancellationToken = default);

    /// <summary>Critical + High severity OPEN exceptions (the audit-committee material-findings widget).</summary>
    Task<IReadOnlyList<MaterialFindingDto>> MaterialFindingsAsync(CancellationToken cancellationToken = default);

    Task<PlanStatusDto> PlanStatusAsync(CancellationToken cancellationToken = default);
}

// ---- KPI DTOs (snake-cased at the API edge by the serializer; these are the shape the port returns) ----

/// <summary>Function-performance KPIs (US-M9 G1): plan execution, in-flight work and exception throughput.</summary>
public sealed record FunctionPerformanceDto(
    int AuditsInFlight,
    int AuditsCompleted,
    int PlanItemsTotal,
    int PlanItemsCompleted,
    decimal PlanExecutionPercent,
    int OpenExceptionBacklog,
    int ClosedExceptions,
    decimal ClosureRatePercent);

/// <summary>One open-exception count bucketed by age (days since raised).</summary>
public sealed record ExceptionAgeBucketDto(string Bucket, int Count);

/// <summary>One open-exception count for a severity tier.</summary>
public sealed record ExceptionSeverityCountDto(string Severity, int Count);

/// <summary>Open exceptions and average closure time for one auditable entity.</summary>
public sealed record ExceptionByEntityDto(Guid AuditableEntityId, string EntityName, int OpenCount, double? AverageClosureDays);

/// <summary>Exception-portfolio KPIs (US-M9 G2): open by severity / by age bucket / by entity + overall closure time.</summary>
public sealed record ExceptionPortfolioDto(
    int TotalOpen,
    IReadOnlyList<ExceptionSeverityCountDto> BySeverity,
    IReadOnlyList<ExceptionAgeBucketDto> ByAgeBucket,
    IReadOnlyList<ExceptionByEntityDto> ByEntity,
    double? AverageClosureDays);

/// <summary>Sanctions consistency for one business unit (category). Subject identity is intentionally absent.</summary>
public sealed record SanctionsConsistencyRowDto(
    string BusinessUnit,
    int CaseCount,
    int WithinGridCount,
    decimal GridAdherencePercent,
    int DeviationCount,
    int AppealCount,
    decimal AppealRatePercent);

/// <summary>Sanctions consistency KPI (US-M9 G3). Aggregated by business unit; NO subject_user_id anywhere.</summary>
public sealed record SanctionsConsistencyDto(
    int TotalCases,
    decimal OverallGridAdherencePercent,
    decimal OverallAppealRatePercent,
    IReadOnlyList<SanctionsConsistencyRowDto> ByBusinessUnit);

/// <summary>Per-audit-lead performance scorecard (US-M9 G4): throughput, cycle time and closure metrics.</summary>
public sealed record PerformanceScorecardDto(
    Guid AuditLeadUserId,
    int AuditsLed,
    int AuditsCompleted,
    double? AverageCycleDays,
    int ExceptionsRaised,
    int ExceptionsClosed,
    double? AverageExceptionClosureDays);

/// <summary>A single Critical/High open exception for the audit-committee material-findings widget.</summary>
public sealed record MaterialFindingDto(
    Guid ExceptionId,
    Guid AuditId,
    string Title,
    string Severity,
    string Status,
    Guid? AuditableEntityId,
    DateTimeOffset RaisedAt,
    DateOnly TargetDate);

/// <summary>Annual-plan execution status (US-M9 G5) — counts of plan items by lifecycle state + completion %.</summary>
public sealed record PlanStatusDto(
    int TotalPlans,
    int TotalItems,
    int Planned,
    int InProgress,
    int Completed,
    int Deferred,
    decimal CompletionPercent);
