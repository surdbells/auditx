using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Analytics;

/// <summary>
/// A single widget on a <see cref="Dashboard"/> (M9). Belongs to its dashboard (FK, cascade). <see cref="MetricKey"/>
/// names the KPI query that supplies its data; <see cref="TargetRoleId"/> scopes a widget to a role (null = bank-wide,
/// shown to everyone who can see the dashboard). <see cref="ConfigJson"/> carries free-form presentation options.
/// </summary>
public sealed class DashboardWidget : Entity
{
    private DashboardWidget()
    {
    }

    internal DashboardWidget(Guid dashboardId, WidgetType widgetType, string metricKey, string title, Guid? targetRoleId, int position, string? configJson)
    {
        DashboardId = dashboardId;
        WidgetType = widgetType;
        MetricKey = Guard.NotNullOrWhiteSpace(metricKey, "dashboard.metric_key_required", "A widget metric key is required.").Trim();
        Title = Guard.NotNullOrWhiteSpace(title, "dashboard.widget_title_required", "A widget title is required.");
        TargetRoleId = targetRoleId;
        Position = position;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? null : configJson;
    }

    public Guid DashboardId { get; private set; }

    public WidgetType WidgetType { get; private set; }

    /// <summary>The KPI key driving this widget's data (e.g. <c>function_performance</c>, <c>material_findings</c>).</summary>
    public string MetricKey { get; private set; } = null!;

    /// <summary>Role the widget is scoped to (null = bank-wide).</summary>
    public Guid? TargetRoleId { get; private set; }

    public int Position { get; private set; }

    public string Title { get; private set; } = null!;

    public string? ConfigJson { get; private set; }

    public byte[] Version { get; private set; } = [];

    internal void Update(WidgetType widgetType, string metricKey, string title, Guid? targetRoleId, int position, string? configJson)
    {
        WidgetType = widgetType;
        MetricKey = Guard.NotNullOrWhiteSpace(metricKey, "dashboard.metric_key_required", "A widget metric key is required.").Trim();
        Title = Guard.NotNullOrWhiteSpace(title, "dashboard.widget_title_required", "A widget title is required.");
        TargetRoleId = targetRoleId;
        Position = position;
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? null : configJson;
    }
}
