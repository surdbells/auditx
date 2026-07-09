using AuditX.Domain.Common;

namespace AuditX.Domain.Analytics;

/// <summary>
/// One point in a KPI time-series: the value of a metric (optionally sliced by a single dimension value) captured
/// as-of a given date. The analytics KPI projections are otherwise computed live/point-in-time, so this append-only
/// fact table is what makes trend and time-series reporting possible. Written by the daily snapshot job.
/// </summary>
public sealed class AnalyticsSnapshot : Entity
{
    private AnalyticsSnapshot()
    {
    }

    /// <summary>The metric identifier, e.g. <c>exceptions.total_open</c> (see <see cref="AnalyticsMetricKeys"/>).</summary>
    public string MetricKey { get; private set; } = null!;

    /// <summary>Optional slice within the metric (e.g. a severity tier for <c>exceptions.open_by_severity</c>); null = headline value.</summary>
    public string? Dimension { get; private set; }

    public decimal Value { get; private set; }

    /// <summary>The date the value describes (UTC calendar day).</summary>
    public DateOnly AsOfDate { get; private set; }

    public static AnalyticsSnapshot Capture(string metricKey, string? dimension, decimal value, DateOnly asOfDate) => new()
    {
        MetricKey = Guard.NotNullOrWhiteSpace(metricKey, "analytics.metric_key_required", "Metric key is required."),
        Dimension = string.IsNullOrWhiteSpace(dimension) ? null : dimension.Trim(),
        Value = value,
        AsOfDate = asOfDate,
    };
}
