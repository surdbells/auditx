namespace AuditX.Api.Contracts;

/// <summary>Add a widget to a dashboard (ConfigureDashboards). <c>Version</c> echoes the dashboard rowversion.</summary>
public sealed record AddDashboardWidgetRequest(
    string WidgetType, string MetricKey, string Title, Guid? TargetRoleId, int Position, string? ConfigJson, string Version);

/// <summary>Edit an existing widget (ConfigureDashboards). <c>Version</c> echoes the dashboard rowversion.</summary>
public sealed record UpdateDashboardWidgetRequest(
    string WidgetType, string MetricKey, string Title, Guid? TargetRoleId, int Position, string? ConfigJson, string Version);

/// <summary>Remove a widget (ConfigureDashboards). <c>Version</c> echoes the dashboard rowversion.</summary>
public sealed record RemoveDashboardWidgetRequest(string Version);
