using AuditX.Domain.Analytics;
using AuditX.Domain.Analytics.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Analytics;

public sealed class RecurrenceClusterTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static List<Guid> Ids(int n) => Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

    [Fact]
    public void Create_at_or_above_threshold_raises_detected_event()
    {
        var members = Ids(3);
        var cluster = RecurrenceCluster.Create(Guid.NewGuid(), "control_gap", 24, members, Now.AddMonths(-6), Now, Now);

        Assert.Equal(3, cluster.ClosedExceptionCount);
        Assert.NotNull(cluster.NotifiedAt);
        Assert.Contains(cluster.DomainEvents, e => e is RecurrenceClusterDetectedEvent);
    }

    [Fact]
    public void Create_below_threshold_does_not_raise_event()
    {
        var cluster = RecurrenceCluster.Create(Guid.NewGuid(), "control_gap", 24, Ids(2), Now.AddMonths(-2), Now, Now);

        Assert.Equal(2, cluster.ClosedExceptionCount);
        Assert.Null(cluster.NotifiedAt);
        Assert.DoesNotContain(cluster.DomainEvents, e => e is RecurrenceClusterDetectedEvent);
    }

    [Fact]
    public void Refresh_re_crossing_the_threshold_re_raises_the_event()
    {
        // Start below threshold (no event), then a refresh pushes it to/over threshold → newly detected.
        var cluster = RecurrenceCluster.Create(Guid.NewGuid(), "control_gap", 24, Ids(2), Now.AddMonths(-2), Now, Now);
        cluster.ClearDomainEvents();

        cluster.Refresh(Ids(4), Now.AddMonths(-3), Now, 24, Now);

        Assert.Equal(4, cluster.ClosedExceptionCount);
        Assert.Contains(cluster.DomainEvents, e => e is RecurrenceClusterDetectedEvent);
    }

    [Fact]
    public void Refresh_that_stays_above_threshold_does_not_re_raise()
    {
        var cluster = RecurrenceCluster.Create(Guid.NewGuid(), "control_gap", 24, Ids(3), Now.AddMonths(-3), Now, Now);
        cluster.ClearDomainEvents();

        cluster.Refresh(Ids(5), Now.AddMonths(-4), Now, 24, Now);

        Assert.Equal(5, cluster.ClosedExceptionCount);
        Assert.DoesNotContain(cluster.DomainEvents, e => e is RecurrenceClusterDetectedEvent);
    }

    [Fact]
    public void Refresh_downgrading_below_threshold_emits_nothing_and_keeps_the_row()
    {
        // The daily scan downgrades a cluster whose members have aged out of the window.
        var cluster = RecurrenceCluster.Create(Guid.NewGuid(), "control_gap", 24, Ids(4), Now.AddMonths(-5), Now, Now);
        cluster.ClearDomainEvents();

        cluster.Refresh([], cluster.FirstOccurredAt, cluster.LastOccurredAt, 24, Now);

        Assert.Equal(0, cluster.ClosedExceptionCount);
        Assert.DoesNotContain(cluster.DomainEvents, e => e is RecurrenceClusterDetectedEvent);
    }

    [Fact]
    public void Blank_category_normalises_to_null()
    {
        var cluster = RecurrenceCluster.Create(Guid.NewGuid(), "   ", 24, Ids(3), Now.AddMonths(-1), Now, Now);
        Assert.Null(cluster.Category);
    }
}

public sealed class DashboardTests
{
    [Fact]
    public void Adding_a_widget_bumps_the_configuration_version()
    {
        var dashboard = Dashboard.Create("exception_portfolio", "Exception Portfolio", null, null, isSystemDefault: true);
        Assert.Equal(1, dashboard.ConfigurationVersion);

        dashboard.AddWidget(WidgetType.Chart, "exception_portfolio", "By severity", targetRoleId: null, position: 0, configJson: null);

        Assert.Single(dashboard.Widgets);
        Assert.Equal(2, dashboard.ConfigurationVersion);
    }

    [Fact]
    public void Editing_and_removing_widgets_keep_bumping_the_version()
    {
        var dashboard = Dashboard.Create("coverage", "Coverage", null, null, isSystemDefault: true);
        var widget = dashboard.AddWidget(WidgetType.Table, "coverage", "Matrix", null, 0, null);

        dashboard.EditWidget(widget.Id, WidgetType.SingleMetric, "coverage", "Coverage %", null, 0, null);
        Assert.Equal(3, dashboard.ConfigurationVersion);

        dashboard.RemoveWidget(widget.Id);
        Assert.Empty(dashboard.Widgets);
        Assert.Equal(4, dashboard.ConfigurationVersion);
    }

    [Fact]
    public void Removing_an_unknown_widget_throws()
    {
        var dashboard = Dashboard.Create("coverage", "Coverage", null, null, isSystemDefault: true);
        Assert.Throws<DomainException>(() => dashboard.RemoveWidget(Guid.NewGuid()));
    }

    [Fact]
    public void Slug_is_normalised_lowercase()
    {
        var dashboard = Dashboard.Create("Function_Performance", "Function Performance", null,
            permissionRequired: "PerformanceAnalyticsView", isSystemDefault: true);
        Assert.Equal("function_performance", dashboard.Slug);
        Assert.Equal("PerformanceAnalyticsView", dashboard.PermissionRequired);
    }
}
