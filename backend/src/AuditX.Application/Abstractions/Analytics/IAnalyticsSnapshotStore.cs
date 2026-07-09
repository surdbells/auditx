using AuditX.Domain.Analytics;

namespace AuditX.Application.Abstractions.Analytics;

/// <summary>One value in a metric time-series.</summary>
public sealed record MetricPoint(DateOnly AsOfDate, decimal Value);

/// <summary>Persistence + read port for the append-only <see cref="AnalyticsSnapshot"/> KPI fact table.</summary>
public interface IAnalyticsSnapshotStore
{
    /// <summary>
    /// Replace the snapshot rows for <paramref name="asOfDate"/> with <paramref name="rows"/> (delete-then-insert),
    /// so a same-day re-run is idempotent. Self-contained: commits its own write.
    /// </summary>
    Task ReplaceForDateAsync(DateOnly asOfDate, IReadOnlyList<AnalyticsSnapshot> rows, CancellationToken cancellationToken = default);

    /// <summary>Ordered time-series for a metric (optionally a single dimension slice) within an inclusive date range.</summary>
    Task<IReadOnlyList<MetricPoint>> GetTrendAsync(
        string metricKey, string? dimension, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
