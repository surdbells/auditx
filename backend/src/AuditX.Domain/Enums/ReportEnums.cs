namespace AuditX.Domain.Enums;

/// <summary>
/// Whether an engagement report version is a DRAFT — for discussion with management, who comment on it — or the
/// FINAL report, which the Chief Internal Auditor signs off before it is distributed. Standalone reports are final.
/// </summary>
public enum ReportStage
{
    Final,
    Draft,
}

/// <summary>
/// Where a final engagement report stands in its sign-off. <see cref="NotRequired"/> for drafts, standalone reports
/// and reports issued before sign-off existed; a new final report starts <see cref="Awaiting"/> and is either
/// <see cref="SignedOff"/> (it may then be distributed) or <see cref="Returned"/> for changes (a new version follows).
/// </summary>
public enum ReportSignOffStatus
{
    NotRequired,
    Awaiting,
    SignedOff,
    Returned,
}

/// <summary>
/// The audit-report generation lifecycle (M8). Persisted snake_case (via <c>SnakeCaseEnumConverter</c>). The
/// lifecycle IS the status (not a rich state machine): <c>pending → running → {completed | failed}</c>. A
/// completed report is content-immutable — there is no edit and no further transition.
/// </summary>
public enum ReportStatus
{
    Pending,
    Running,
    Completed,
    Failed,

    /// <summary>Retention period reached: the immutable record + hash are kept, but the artefacts are no longer served.</summary>
    Expired,
}

/// <summary>
/// The kind of report (M8). <see cref="AuditEngagement"/> (default 0) is the per-audit engagement report; the
/// remaining kinds are standalone / cross-audit reports (no single audit) rendered from the M9 analytics.
/// </summary>
public enum ReportKind
{
    AuditEngagement = 0,
    ExecutiveSummary,
    AnnualPlanStatus,
    KpiPack,
    AuditCoverage,
    FindingsRegister,
    SanctionsConsistency,
    PerformanceScorecards,
}

/// <summary>
/// The delivery outcome of a single report distribution (M8). Defaults to <see cref="Pending"/>; the inbound
/// M14 relay callback that flips it to <see cref="Delivered"/>/<see cref="Bounced"/> is deferred.
/// </summary>
public enum DeliveryOutcome
{
    Pending,
    Delivered,
    Bounced,
}
