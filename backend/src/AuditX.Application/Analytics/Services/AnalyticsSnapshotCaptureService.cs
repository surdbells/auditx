using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Domain.Analytics;

namespace AuditX.Application.Analytics.Services;

/// <summary>
/// Captures the current KPI projections into the <see cref="AnalyticsSnapshot"/> fact table for today's date.
/// Runs the existing live analytics queries and flattens their headline scalars into (metric, dimension, value)
/// rows, so trend/time-series reporting has a queryable backing without changing the live queries. Idempotent
/// per day (the store replaces the day's rows).
/// </summary>
public sealed class AnalyticsSnapshotCaptureService(
    IAnalyticsQueryService analytics, IAnalyticsSnapshotStore store, IClock clock)
{
    /// <summary>Captures today's snapshot; returns the number of rows written.</summary>
    public async Task<int> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var asOf = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var function = await analytics.FunctionPerformanceAsync(cancellationToken);
        var portfolio = await analytics.ExceptionPortfolioAsync(cancellationToken);
        var plan = await analytics.PlanStatusAsync(cancellationToken);
        var risk = await analytics.RiskSummaryAsync(cancellationToken);
        var riskHeatmap = await analytics.RiskHeatmapAsync(cancellationToken);

        var rows = new List<AnalyticsSnapshot>
        {
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.AuditsInFlight, null, function.AuditsInFlight, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.AuditsCompleted, null, function.AuditsCompleted, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.PlanExecutionPercent, null, function.PlanExecutionPercent, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.OpenExceptionBacklog, null, function.OpenExceptionBacklog, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ClosedExceptions, null, function.ClosedExceptions, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ClosureRatePercent, null, function.ClosureRatePercent, asOf),

            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ExceptionsTotalOpen, null, portfolio.TotalOpen, asOf),

            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.PlanCompletionPercent, null, plan.CompletionPercent, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.PlanItemsTotal, null, plan.TotalItems, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.PlanItemsCompleted, null, plan.Completed, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.PlanItemsInProgress, null, plan.InProgress, asOf),

            // Enterprise risk register (P1-A): headline open count + overdue reviews give the risk trend its backbone.
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.RiskOpenTotal, null, risk.Open, asOf),
            AnalyticsSnapshot.Capture(AnalyticsMetricKeys.RiskOverdueReview, null, risk.OverdueReview, asOf),
        };

        // Average closure time is optional (null until an exception closes).
        if (portfolio.AverageClosureDays is { } avg)
        {
            rows.Add(AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ExceptionsAvgClosureDays, null, (decimal)avg, asOf));
        }

        // Open exceptions sliced by severity — one row per tier.
        foreach (var tier in portfolio.BySeverity)
        {
            rows.Add(AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ExceptionsOpenBySeverity, tier.Severity, tier.Count, asOf));
        }

        // Open risks sliced by band — one row per band (mirrors the by-severity slice).
        foreach (var band in risk.ByBand)
        {
            rows.Add(AnalyticsSnapshot.Capture(AnalyticsMetricKeys.RiskOpenByBand, band.Key, band.Count, asOf));
        }

        // Mean current (residual-else-inherent) score across open risks — the severity-drift line. Undefined when no
        // risk is open, so omit the row (like average closure) rather than record a misleading zero.
        if (riskHeatmap.TotalOpen > 0)
        {
            var weightedScore = riskHeatmap.Cells.Sum(c => (decimal)c.Score * c.Count);
            rows.Add(AnalyticsSnapshot.Capture(
                AnalyticsMetricKeys.RiskAvgCurrentScore, null, Math.Round(weightedScore / riskHeatmap.TotalOpen, 2), asOf));
        }

        await store.ReplaceForDateAsync(asOf, rows, cancellationToken);
        return rows.Count;
    }
}
