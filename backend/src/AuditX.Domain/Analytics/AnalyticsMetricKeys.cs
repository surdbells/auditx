namespace AuditX.Domain.Analytics;

/// <summary>
/// The canonical set of metric keys captured into <see cref="AnalyticsSnapshot"/> by the daily job and exposed by
/// the trend endpoint. Stable strings — the frontend charts reference these directly, so treat renames as breaking.
/// </summary>
public static class AnalyticsMetricKeys
{
    // ---- Function performance (headline) ----
    public const string AuditsInFlight = "function.audits_in_flight";
    public const string AuditsCompleted = "function.audits_completed";
    public const string PlanExecutionPercent = "function.plan_execution_pct";
    public const string OpenExceptionBacklog = "function.open_exception_backlog";
    public const string ClosedExceptions = "function.closed_exceptions";
    public const string ClosureRatePercent = "function.closure_rate_pct";

    // ---- Exception portfolio ----
    public const string ExceptionsTotalOpen = "exceptions.total_open";
    public const string ExceptionsAvgClosureDays = "exceptions.avg_closure_days";

    /// <summary>Open exceptions sliced by severity — the <c>Dimension</c> carries the severity tier (e.g. <c>critical</c>).</summary>
    public const string ExceptionsOpenBySeverity = "exceptions.open_by_severity";

    // ---- Plan status ----
    public const string PlanCompletionPercent = "plan.completion_pct";
    public const string PlanItemsTotal = "plan.items_total";
    public const string PlanItemsCompleted = "plan.items_completed";
    public const string PlanItemsInProgress = "plan.items_in_progress";
}
