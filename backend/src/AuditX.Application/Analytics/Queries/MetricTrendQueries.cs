using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Analytics.Queries;

// ---- KPI metric trend / time-series (ViewAnalytics) ----

public sealed record MetricPointDto(DateOnly AsOfDate, decimal Value);

public sealed record MetricTrendDto(string MetricKey, string? Dimension, IReadOnlyList<MetricPointDto> Points);

/// <summary>Time-series for a snapshot metric. Defaults to the trailing 90 days when no range is given.</summary>
public sealed record GetMetricTrendQuery(string MetricKey, string? Dimension, DateOnly? From, DateOnly? To)
    : IQuery<MetricTrendDto>;

public sealed class GetMetricTrendQueryHandler(IAnalyticsSnapshotStore store, IClock clock)
    : IQueryHandler<GetMetricTrendQuery, MetricTrendDto>
{
    private const int DefaultWindowDays = 90;

    public async Task<MetricTrendDto> Handle(GetMetricTrendQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-DefaultWindowDays);

        var points = await store.GetTrendAsync(query.MetricKey, query.Dimension, from, to, cancellationToken);
        return new MetricTrendDto(
            query.MetricKey, query.Dimension, points.Select(p => new MetricPointDto(p.AsOfDate, p.Value)).ToArray());
    }
}
