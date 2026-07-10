using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Analytics.Services;
using AuditX.Domain.Analytics;
using NSubstitute;

namespace AuditX.Application.Tests.Analytics;

public sealed class AnalyticsSnapshotCaptureServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 8, 0, 0, TimeSpan.Zero);

    private static (AnalyticsSnapshotCaptureService svc, IAnalyticsSnapshotStore store) Build(
        ExceptionPortfolioDto portfolio)
    {
        var analytics = Substitute.For<IAnalyticsQueryService>();
        analytics.FunctionPerformanceAsync(Arg.Any<CancellationToken>())
            .Returns(new FunctionPerformanceDto(3, 5, 10, 6, 60m, 8, 12, 60m));
        analytics.ExceptionPortfolioAsync(Arg.Any<CancellationToken>()).Returns(portfolio);
        analytics.PlanStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new PlanStatusDto(1, 10, 2, 3, 5, 0, 50m));

        var store = Substitute.For<IAnalyticsSnapshotStore>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        return (new AnalyticsSnapshotCaptureService(analytics, store, clock), store);
    }

    private static IReadOnlyList<AnalyticsSnapshot> Captured(IAnalyticsSnapshotStore store)
    {
        var call = store.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IAnalyticsSnapshotStore.ReplaceForDateAsync));
        return (IReadOnlyList<AnalyticsSnapshot>)call.GetArguments()[1]!;
    }

    private static decimal ValueOf(IReadOnlyList<AnalyticsSnapshot> rows, string key, string? dim = null)
        => rows.Single(r => r.MetricKey == key && r.Dimension == dim).Value;

    [Fact]
    public async Task Captures_headline_metrics_for_today()
    {
        var (svc, store) = Build(new ExceptionPortfolioDto(9, [], [], [], [], 4.5));

        var count = await svc.CaptureAsync();

        await store.Received(1).ReplaceForDateAsync(new DateOnly(2026, 7, 9), Arg.Any<IReadOnlyList<AnalyticsSnapshot>>(), Arg.Any<CancellationToken>());
        var rows = Captured(store);
        Assert.All(rows, r => Assert.Equal(new DateOnly(2026, 7, 9), r.AsOfDate));
        Assert.Equal(3m, ValueOf(rows, AnalyticsMetricKeys.AuditsInFlight));
        Assert.Equal(60m, ValueOf(rows, AnalyticsMetricKeys.PlanExecutionPercent));
        Assert.Equal(9m, ValueOf(rows, AnalyticsMetricKeys.ExceptionsTotalOpen));
        Assert.Equal(50m, ValueOf(rows, AnalyticsMetricKeys.PlanCompletionPercent));
        Assert.Equal(rows.Count, count);
    }

    [Fact]
    public async Task Captures_a_row_per_severity_slice()
    {
        var (svc, store) = Build(new ExceptionPortfolioDto(
            9,
            [new ExceptionSeverityCountDto("critical", 2), new ExceptionSeverityCountDto("high", 4)],
            [], [], [], 4.5));

        await svc.CaptureAsync();
        var rows = Captured(store);

        Assert.Equal(2m, ValueOf(rows, AnalyticsMetricKeys.ExceptionsOpenBySeverity, "critical"));
        Assert.Equal(4m, ValueOf(rows, AnalyticsMetricKeys.ExceptionsOpenBySeverity, "high"));
        Assert.Equal(4.5m, ValueOf(rows, AnalyticsMetricKeys.ExceptionsAvgClosureDays));
    }

    [Fact]
    public async Task Omits_average_closure_when_no_exception_has_closed()
    {
        var (svc, store) = Build(new ExceptionPortfolioDto(9, [], [], [], [], AverageClosureDays: null));

        await svc.CaptureAsync();
        var rows = Captured(store);

        Assert.DoesNotContain(rows, r => r.MetricKey == AnalyticsMetricKeys.ExceptionsAvgClosureDays);
    }
}
