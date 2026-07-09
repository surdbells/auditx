namespace AuditX.Domain.Enums;

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
