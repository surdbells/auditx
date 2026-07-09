using AuditX.Domain.Analytics;
using AuditX.Domain.Common;

namespace AuditX.Domain.Tests.Analytics;

public sealed class AnalyticsSnapshotTests
{
    private static readonly DateOnly AsOf = new(2026, 7, 9);

    [Fact]
    public void Capture_sets_the_fields()
    {
        var s = AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ExceptionsTotalOpen, null, 12m, AsOf);

        Assert.Equal(AnalyticsMetricKeys.ExceptionsTotalOpen, s.MetricKey);
        Assert.Null(s.Dimension);
        Assert.Equal(12m, s.Value);
        Assert.Equal(AsOf, s.AsOfDate);
        Assert.NotEqual(Guid.Empty, s.Id);
    }

    [Theory]
    [InlineData("critical", "critical")]
    [InlineData("  high  ", "high")]
    public void Capture_trims_the_dimension(string input, string expected)
    {
        var s = AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ExceptionsOpenBySeverity, input, 3m, AsOf);
        Assert.Equal(expected, s.Dimension);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Capture_normalises_a_blank_dimension_to_null(string input)
    {
        var s = AnalyticsSnapshot.Capture(AnalyticsMetricKeys.ExceptionsOpenBySeverity, input, 3m, AsOf);
        Assert.Null(s.Dimension);
    }

    [Fact]
    public void Capture_requires_a_metric_key()
    {
        Assert.Throws<DomainException>(() => AnalyticsSnapshot.Capture("  ", null, 1m, AsOf));
    }
}
