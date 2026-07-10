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

    /// <summary>
    /// Department / business-unit scorecards: audits and findings rolled up the OrgUnit tree (each row aggregates the
    /// unit itself PLUS every descendant unit). Findings/audits inherit their org unit via the auditable entity.
    /// Powers the department-performance, risk-by-BU and coverage-by-BU board reports.
    /// </summary>
    Task<IReadOnlyList<OrgUnitScorecardDto>> OrgUnitScorecardsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Budget-vs-actual per audit (P0-B): budgeted hours (baseline) vs the sum of logged time entries, with the
    /// variance and % consumed. Only audits with either a budget or logged time appear. Powers effort/cost reporting.
    /// </summary>
    Task<IReadOnlyList<BudgetVsActualDto>> BudgetVsActualAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Utilisation per auditor (P0-B): total logged hours, audits contributed to, and the hours split by activity
    /// category. Powers the resource-utilisation board report.
    /// </summary>
    Task<IReadOnlyList<AuditorUtilisationDto>> UtilisationByUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Auditor workload vs capacity: open (Planned / InProgress) plan-item effort rolled up by assigned lead and
    /// compared against the lead's declared capacity (person-days). Every lead with open work AND every user with a
    /// declared capacity appears (so under-committed auditors surface too). Optionally scoped to one annual plan.
    /// Powers the resource-planning / over-commitment board report.
    /// </summary>
    Task<IReadOnlyList<AuditorWorkloadDto>> AuditorWorkloadAsync(Guid? annualPlanId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enterprise risk heatmap (P1-A): open (non-closed) risks bucketed by their CURRENT (residual, else inherent)
    /// likelihood×impact cell on the 5×5 matrix. Powers the risk-heatmap board report.
    /// </summary>
    Task<RiskHeatmapDto> RiskHeatmapAsync(CancellationToken cancellationToken = default);

    /// <summary>Risk-register roll-up (P1-A): totals + open risks split by band, status, category and treatment strategy.</summary>
    Task<RiskRegisterSummaryDto> RiskSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Control-effectiveness roll-up (P1-B): active controls split by tested effectiveness and type.</summary>
    Task<ControlEffectivenessSummaryDto> ControlEffectivenessAsync(CancellationToken cancellationToken = default);

    /// <summary>Compliance-by-regulation (P1-B): per active regulation, the count of linked + open findings.</summary>
    Task<IReadOnlyList<ComplianceByRegulationRowDto>> ComplianceByRegulationAsync(CancellationToken cancellationToken = default);

    /// <summary>Finding follow-up summary (P2-B): management-response coverage, reopen count and verification outcomes.</summary>
    Task<FindingFollowUpSummaryDto> FindingFollowUpAsync(CancellationToken cancellationToken = default);

    /// <summary>Execution-procedure coverage + sampling error-rate (P2-C).</summary>
    Task<ProcedureSummaryDto> ProcedureSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Requested-vs-received evidence: outstanding / overdue / waived counts + by document type (P2-D).</summary>
    Task<EvidenceSummaryDto> EvidenceSummaryAsync(CancellationToken cancellationToken = default);
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

/// <summary>Open exceptions grouped by their root-cause taxonomy code (P2-A). "uncategorised" collects blanks.</summary>
public sealed record ExceptionRootCauseCountDto(string RootCauseCategory, int Count);

/// <summary>Exception-portfolio KPIs (US-M9 G2): open by severity / by age bucket / by entity / by root cause + overall closure time.</summary>
public sealed record ExceptionPortfolioDto(
    int TotalOpen,
    IReadOnlyList<ExceptionSeverityCountDto> BySeverity,
    IReadOnlyList<ExceptionAgeBucketDto> ByAgeBucket,
    IReadOnlyList<ExceptionByEntityDto> ByEntity,
    IReadOnlyList<ExceptionRootCauseCountDto> ByRootCause,
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

/// <summary>
/// A department / business-unit scorecard: audits + findings rolled up an OrgUnit and its whole subtree. <c>Depth</c>
/// is the unit's depth in the tree (0 = a root) for indented rendering.
/// </summary>
public sealed record OrgUnitScorecardDto(
    Guid OrgUnitId,
    string Code,
    string Name,
    Guid? ParentOrgUnitId,
    int Depth,
    int Entities,
    int AuditsCompleted,
    int AuditsInFlight,
    int OpenFindings,
    int CriticalOpenFindings,
    int HighOpenFindings,
    int ClosedFindings,
    double? AverageClosureDays);

/// <summary>Budget-vs-actual for one audit (P0-B). Variance/percent are null when no budget is set.</summary>
public sealed record BudgetVsActualDto(
    Guid AuditId,
    string AuditName,
    string Status,
    Guid LeadUserId,
    decimal? BudgetedHours,
    decimal ActualHours,
    decimal? VarianceHours,
    double? PercentConsumed);

/// <summary>Hours logged in one activity category (for a utilisation split).</summary>
public sealed record UtilisationCategoryDto(string Category, decimal Hours);

/// <summary>Total logged effort for one auditor, split by activity category (P0-B).</summary>
public sealed record AuditorUtilisationDto(
    Guid UserId,
    decimal TotalHours,
    int AuditsContributed,
    IReadOnlyList<UtilisationCategoryDto> ByCategory);

/// <summary>
/// Planned workload vs capacity for one auditor: open planned effort led (person-days) against the lead's declared
/// capacity. <c>UtilisationPercent</c>/<c>OverCommitted</c> are null/false when no capacity is declared.
/// </summary>
public sealed record AuditorWorkloadDto(
    Guid LeadUserId,
    int PlanItemCount,
    decimal PlannedEffortDays,
    decimal? CapacityDays,
    double? UtilisationPercent,
    bool OverCommitted);

/// <summary>One populated cell of the risk heatmap: a likelihood×impact position with its band and risk count.</summary>
public sealed record RiskHeatmapCellDto(int Likelihood, int Impact, int Score, string Band, int Count);

/// <summary>The enterprise risk heatmap (P1-A): total open risks + the populated 5×5 cells.</summary>
public sealed record RiskHeatmapDto(int TotalOpen, IReadOnlyList<RiskHeatmapCellDto> Cells);

/// <summary>A labelled count in a risk roll-up (band / status / category / strategy).</summary>
public sealed record RiskCountDto(string Key, int Count);

/// <summary>Risk-register roll-up (P1-A).</summary>
public sealed record RiskRegisterSummaryDto(
    int Total,
    int Open,
    int Closed,
    int OverdueReview,
    IReadOnlyList<RiskCountDto> ByBand,
    IReadOnlyList<RiskCountDto> ByStatus,
    IReadOnlyList<RiskCountDto> ByCategory,
    IReadOnlyList<RiskCountDto> ByStrategy);

/// <summary>A labelled count in a control roll-up (effectiveness / type).</summary>
public sealed record ControlCountDto(string Key, int Count);

/// <summary>Control-effectiveness roll-up (P1-B), over ACTIVE controls.</summary>
public sealed record ControlEffectivenessSummaryDto(
    int TotalActive,
    int Tested,
    int Ineffective,
    IReadOnlyList<ControlCountDto> ByEffectiveness,
    IReadOnlyList<ControlCountDto> ByType);

/// <summary>Compliance-by-regulation row (P1-B): one active regulation + its linked/open finding counts.</summary>
public sealed record ComplianceByRegulationRowDto(
    Guid RegulationId, string Code, string Name, string? Authority, int LinkedFindings, int OpenFindings);

/// <summary>One verification-outcome count for the follow-up summary (P2-B).</summary>
public sealed record VerificationResultCountDto(string Result, int Count);

/// <summary>
/// Finding follow-up summary (P2-B): management-response coverage, reopen count, and post-closure verification
/// outcomes — the substrate for management-timeliness and follow-up-effectiveness reporting.
/// <c>WithResponseDue</c>..<c>ResponseOverdue</c> are the response-SLA slice (findings with a response due date):
/// on-time / late once responded, still-outstanding-and-past-due otherwise. <c>AverageResponseDays</c> is the mean
/// raise→response turnaround over all responded findings.
/// </summary>
public sealed record FindingFollowUpSummaryDto(
    int TotalFindings,
    int Closed,
    int Reopened,
    int WithManagementResponse,
    int VerifiedFindings,
    IReadOnlyList<VerificationResultCountDto> ByVerificationResult,
    int WithResponseDue,
    int RespondedOnTime,
    int RespondedLate,
    int ResponseOverdue,
    double? AverageResponseDays);

/// <summary>One procedure-type count for the procedure summary (P2-C).</summary>
public sealed record ProcedureTypeCountDto(string Type, int Count);

/// <summary>
/// Execution-procedure summary (P2-C): coverage by type plus the aggregate sampling error-rate
/// (total exceptions found / total items tested across all sampling procedures).
/// </summary>
public sealed record ProcedureSummaryDto(
    int TotalProcedures,
    IReadOnlyList<ProcedureTypeCountDto> ByType,
    int SamplingProcedures,
    int TotalItemsTested,
    int TotalExceptionsFound,
    double? SampleErrorRatePercent);

/// <summary>One document-type count for the evidence summary (P2-D); "unspecified" collects blanks.</summary>
public sealed record EvidenceTypeCountDto(string DocumentType, int Count);

/// <summary>Requested-vs-received evidence summary (P2-D): outstanding / overdue / waived + by document type.</summary>
public sealed record EvidenceSummaryDto(
    int TotalRequests,
    int Outstanding,
    int Received,
    int Waived,
    int Overdue,
    IReadOnlyList<EvidenceTypeCountDto> ByDocumentType);
