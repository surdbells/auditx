using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Common;

namespace AuditX.Application.Analytics.Queries;

// ---- Period-over-period comparison + forecast for a snapshot metric (ViewAnalytics) ----

/// <summary>The calendar grain a metric series is bucketed into for period-over-period comparison.</summary>
public enum ComparisonPeriod
{
    Month,
    Quarter,
    Year,
}

/// <summary>One calendar period's representative value (the latest snapshot that falls within the period; null = a gap).</summary>
public sealed record MetricPeriodPointDto(string Label, DateOnly PeriodStart, DateOnly PeriodEnd, decimal? Value);

/// <summary>A projected next-period value from an ordinary-least-squares fit over the daily series.</summary>
public sealed record MetricForecastDto(string Method, DateOnly ProjectedFor, decimal ProjectedValue, decimal Slope);

/// <summary>
/// Period-over-period comparison for a snapshot metric: the trailing <c>Periods</c> calendar buckets (month / quarter /
/// year), the latest-vs-previous delta + percent change (MoM / QoQ / YoY), and a simple linear forecast of the next
/// period. Computed from the append-only <see cref="AuditX.Domain.Analytics.AnalyticsSnapshot"/> daily series — no live
/// recomputation, so it only reflects days the snapshot job has captured.
/// </summary>
public sealed record MetricComparisonDto(
    string MetricKey,
    string? Dimension,
    string Period,
    IReadOnlyList<MetricPeriodPointDto> Periods,
    decimal? Current,
    decimal? Previous,
    decimal? Delta,
    decimal? PercentChange,
    MetricForecastDto? Forecast);

/// <summary>
/// Period-over-period comparison + forecast for a KPI metric. <paramref name="Period"/> is <c>month</c>, <c>quarter</c>
/// or <c>year</c>; <paramref name="Periods"/> is how many trailing buckets to return (defaulted + capped per grain).
/// </summary>
public sealed record GetMetricComparisonQuery(string MetricKey, string? Dimension, string Period, int? Periods)
    : IQuery<MetricComparisonDto>;

public sealed class GetMetricComparisonQueryHandler(IAnalyticsSnapshotStore store, IClock clock)
    : IQueryHandler<GetMetricComparisonQuery, MetricComparisonDto>
{
    private const int MaxPeriods = 36;

    public async Task<MetricComparisonDto> Handle(GetMetricComparisonQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.MetricKey))
        {
            throw new DomainException("analytics.metric_key_required", "A metric key is required.");
        }

        var period = ParsePeriod(query.Period);
        var count = ClampPeriods(query.Periods, period);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        // The buckets are the `count` calendar periods ending with the one that contains today.
        var buckets = BuildBuckets(today, period, count);
        var from = buckets[0].Start;

        var points = await store.GetTrendAsync(query.MetricKey, NormaliseDimension(query.Dimension), from, today, cancellationToken);

        // Representative value per bucket = the latest daily point that falls within the bucket (period-end level).
        var periodPoints = buckets
            .Select(b =>
            {
                var value = points
                    .Where(p => p.AsOfDate >= b.Start && p.AsOfDate <= b.End)
                    .OrderBy(p => p.AsOfDate)
                    .Select(p => (decimal?)p.Value)
                    .LastOrDefault();
                return new MetricPeriodPointDto(b.Label, b.Start, b.End, value);
            })
            .ToArray();

        // Latest-vs-previous over buckets that actually have a value (skip gaps so a missing month doesn't read as 0).
        var valued = periodPoints.Where(p => p.Value is not null).ToArray();
        decimal? current = valued.Length >= 1 ? valued[^1].Value : null;
        decimal? previous = valued.Length >= 2 ? valued[^2].Value : null;
        decimal? delta = current is not null && previous is not null ? current - previous : null;
        decimal? percentChange = delta is not null && previous is not null && previous.Value != 0m
            ? Math.Round(delta.Value / previous.Value * 100m, 2)
            : null;

        var forecast = BuildForecast(points, today, period);

        return new MetricComparisonDto(
            query.MetricKey, NormaliseDimension(query.Dimension), period.ToString().ToLowerInvariant(),
            periodPoints, current, previous, delta, percentChange, forecast);
    }

    private static string? NormaliseDimension(string? dimension)
        => string.IsNullOrWhiteSpace(dimension) ? null : dimension.Trim();

    private static ComparisonPeriod ParsePeriod(string? period)
        => (period?.Trim().ToLowerInvariant()) switch
        {
            "month" or "mom" => ComparisonPeriod.Month,
            "quarter" or "qoq" => ComparisonPeriod.Quarter,
            "year" or "yoy" => ComparisonPeriod.Year,
            _ => throw new DomainException("analytics.invalid_period", "Period must be one of: month, quarter, year."),
        };

    private static int ClampPeriods(int? requested, ComparisonPeriod period)
    {
        var fallback = period switch
        {
            ComparisonPeriod.Month => 12,
            ComparisonPeriod.Quarter => 8,
            _ => 5,
        };
        var value = requested ?? fallback;
        return Math.Clamp(value, 2, MaxPeriods);
    }

    /// <summary>The `count` calendar buckets ending with the one containing <paramref name="today"/>, oldest first.</summary>
    private static List<(DateOnly Start, DateOnly End, string Label)> BuildBuckets(DateOnly today, ComparisonPeriod period, int count)
    {
        var buckets = new List<(DateOnly Start, DateOnly End, string Label)>(count);
        for (var offset = count - 1; offset >= 0; offset--)
        {
            buckets.Add(BucketFor(today, period, offset));
        }

        return buckets;
    }

    /// <summary>The calendar bucket <paramref name="offset"/> periods before the one containing <paramref name="today"/>.</summary>
    private static (DateOnly Start, DateOnly End, string Label) BucketFor(DateOnly today, ComparisonPeriod period, int offset)
    {
        switch (period)
        {
            case ComparisonPeriod.Month:
            {
                var anchor = new DateOnly(today.Year, today.Month, 1).AddMonths(-offset);
                var end = anchor.AddMonths(1).AddDays(-1);
                return (anchor, end, anchor.ToString("yyyy-MM"));
            }

            case ComparisonPeriod.Quarter:
            {
                var currentQuarter = (today.Month - 1) / 3; // 0..3
                var startMonth = currentQuarter * 3 + 1;
                var anchor = new DateOnly(today.Year, startMonth, 1).AddMonths(-3 * offset);
                var end = anchor.AddMonths(3).AddDays(-1);
                var q = (anchor.Month - 1) / 3 + 1;
                return (anchor, end, $"Q{q} {anchor.Year}");
            }

            default: // Year
            {
                var anchor = new DateOnly(today.Year - offset, 1, 1);
                var end = anchor.AddYears(1).AddDays(-1);
                return (anchor, end, anchor.Year.ToString());
            }
        }
    }

    /// <summary>
    /// Ordinary-least-squares fit over the daily points (x = days since the first point), projecting the next period's
    /// end. Returns null when there are fewer than two distinct dates or the series is flat/degenerate.
    /// </summary>
    private static MetricForecastDto? BuildForecast(IReadOnlyList<MetricPoint> points, DateOnly today, ComparisonPeriod period)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var origin = points[0].AsOfDate;
        var xs = points.Select(p => (double)p.AsOfDate.DayNumber - origin.DayNumber).ToArray();
        var ys = points.Select(p => (double)p.Value).ToArray();

        var n = xs.Length;
        var meanX = xs.Average();
        var meanY = ys.Average();
        double sxx = 0, sxy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            sxx += dx * dx;
            sxy += dx * (ys[i] - meanY);
        }

        if (sxx <= double.Epsilon)
        {
            return null; // all points share a date — no slope is defined.
        }

        var slope = sxy / sxx;
        var intercept = meanY - slope * meanX;

        var projectedFor = BucketFor(today, period, -1).End; // the end of the NEXT period.
        var xHorizon = (double)projectedFor.DayNumber - origin.DayNumber;
        var projected = intercept + slope * xHorizon;

        // Every captured KPI (counts, percentages, days) is non-negative, so floor the linear extrapolation at zero
        // rather than surface a nonsensical negative projection from a steep downward trend.
        return new MetricForecastDto(
            "linear_regression",
            projectedFor,
            Math.Max(0m, Math.Round((decimal)projected, 4)),
            Math.Round((decimal)slope, 6));
    }
}
