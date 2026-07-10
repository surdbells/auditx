using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Analytics.Queries;
using AuditX.Domain.Common;
using NSubstitute;

namespace AuditX.Application.Tests.Analytics;

public sealed class MetricComparisonQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    private static GetMetricComparisonQueryHandler Build(params MetricPoint[] points)
    {
        var store = Substitute.For<IAnalyticsSnapshotStore>();
        store.GetTrendAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(points);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        return new GetMetricComparisonQueryHandler(store, clock);
    }

    [Fact]
    public async Task Month_comparison_computes_latest_vs_previous_delta_and_percent()
    {
        var handler = Build(
            new MetricPoint(new DateOnly(2026, 5, 31), 100m),
            new MetricPoint(new DateOnly(2026, 6, 30), 120m),
            new MetricPoint(new DateOnly(2026, 7, 10), 150m));

        var result = await handler.Handle(
            new GetMetricComparisonQuery("exceptions.total_open", null, "month", null), CancellationToken.None);

        Assert.Equal("month", result.Period);
        Assert.Equal(150m, result.Current);   // latest valued bucket = July (latest point within July)
        Assert.Equal(120m, result.Previous);  // previous valued bucket = June
        Assert.Equal(30m, result.Delta);
        Assert.Equal(25m, result.PercentChange);
        // The trailing bucket is the one containing today.
        Assert.Equal("2026-07", result.Periods[^1].Label);
        Assert.Equal(150m, result.Periods[^1].Value);
    }

    [Fact]
    public async Task Empty_periods_are_gaps_not_zero_so_they_are_skipped_for_delta()
    {
        // Only two valued months separated by an empty one — the empty June must NOT read as 0.
        var handler = Build(
            new MetricPoint(new DateOnly(2026, 5, 20), 80m),
            new MetricPoint(new DateOnly(2026, 7, 5), 90m));

        var result = await handler.Handle(
            new GetMetricComparisonQuery("exceptions.total_open", null, "month", 6), CancellationToken.None);

        Assert.Equal(90m, result.Current);
        Assert.Equal(80m, result.Previous);   // skips the empty June bucket
        Assert.Equal(10m, result.Delta);
        Assert.Contains(result.Periods, p => p.Label == "2026-06" && p.Value is null);
    }

    [Fact]
    public async Task Forecast_projects_an_upward_slope_from_a_rising_series()
    {
        var handler = Build(
            new MetricPoint(new DateOnly(2026, 5, 1), 10m),
            new MetricPoint(new DateOnly(2026, 6, 1), 20m),
            new MetricPoint(new DateOnly(2026, 7, 1), 30m));

        var result = await handler.Handle(
            new GetMetricComparisonQuery("function.open_exception_backlog", null, "month", null), CancellationToken.None);

        Assert.NotNull(result.Forecast);
        Assert.Equal("linear_regression", result.Forecast!.Method);
        Assert.True(result.Forecast.Slope > 0);
        Assert.True(result.Forecast.ProjectedValue > 30m);       // extrapolated beyond the last observed value
        Assert.True(result.Forecast.ProjectedFor > new DateOnly(2026, 7, 31)); // the next period's end
    }

    [Fact]
    public async Task Forecast_is_floored_at_zero_for_a_steep_decline()
    {
        // A sharp downward series would extrapolate below zero — nonsensical for a non-negative KPI, so it floors at 0.
        var handler = Build(
            new MetricPoint(new DateOnly(2026, 5, 1), 20m),
            new MetricPoint(new DateOnly(2026, 6, 1), 8m),
            new MetricPoint(new DateOnly(2026, 7, 1), 1m));

        var result = await handler.Handle(
            new GetMetricComparisonQuery("exceptions.total_open", null, "month", null), CancellationToken.None);

        Assert.NotNull(result.Forecast);
        Assert.True(result.Forecast!.Slope < 0);
        Assert.Equal(0m, result.Forecast.ProjectedValue);
    }

    [Fact]
    public async Task Forecast_is_null_with_fewer_than_two_points()
    {
        var handler = Build(new MetricPoint(new DateOnly(2026, 7, 1), 30m));

        var result = await handler.Handle(
            new GetMetricComparisonQuery("function.open_exception_backlog", null, "month", null), CancellationToken.None);

        Assert.Null(result.Forecast);
        Assert.Equal(30m, result.Current);
        Assert.Null(result.Previous);
        Assert.Null(result.Delta);
    }

    [Fact]
    public async Task Year_period_labels_buckets_by_calendar_year()
    {
        var handler = Build(
            new MetricPoint(new DateOnly(2025, 12, 31), 200m),
            new MetricPoint(new DateOnly(2026, 6, 30), 260m));

        var result = await handler.Handle(
            new GetMetricComparisonQuery("exceptions.total_open", null, "year", 3), CancellationToken.None);

        Assert.Equal("year", result.Period);
        Assert.Equal("2026", result.Periods[^1].Label);
        Assert.Equal(260m, result.Current);
        Assert.Equal(200m, result.Previous);
        Assert.Equal(30m, result.PercentChange); // (260-200)/200 * 100
    }

    [Fact]
    public async Task Invalid_period_is_rejected()
    {
        var handler = Build(new MetricPoint(new DateOnly(2026, 7, 1), 30m));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new GetMetricComparisonQuery("exceptions.total_open", null, "weekly", null), CancellationToken.None));
    }
}
