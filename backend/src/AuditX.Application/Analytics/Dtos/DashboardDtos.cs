namespace AuditX.Application.Analytics.Dtos;

/// <summary>A dashboard in the list surface (no widgets/data — just the catalogue entry the caller may open).</summary>
public sealed record DashboardListItemDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string? PermissionRequired,
    int ConfigurationVersion,
    int WidgetCount);

/// <summary>A widget on a dashboard with its computed data payload (null if the KPI key is unknown).</summary>
public sealed record DashboardWidgetDto(
    Guid Id,
    string WidgetType,
    string MetricKey,
    string Title,
    Guid? TargetRoleId,
    int Position,
    string? ConfigJson,
    object? Data,
    string Version);

/// <summary>A fully-assembled dashboard: layout + each visible widget's computed KPI data.</summary>
public sealed record DashboardDetailDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string? PermissionRequired,
    int ConfigurationVersion,
    IReadOnlyList<DashboardWidgetDto> Widgets,
    string Version);

/// <summary>A detected recurrence cluster in the list surface.</summary>
public sealed record RecurrenceClusterDto(
    Guid Id,
    Guid AuditableEntityId,
    string? Category,
    int ClosedExceptionCount,
    int WindowMonths,
    DateTimeOffset FirstOccurredAt,
    DateTimeOffset LastOccurredAt,
    DateTimeOffset DetectedAt,
    DateTimeOffset? NotifiedAt);

/// <summary>A recurrence cluster with its member exceptions (drilldown).</summary>
public sealed record RecurrenceClusterDetailDto(
    Guid Id,
    Guid AuditableEntityId,
    string? Category,
    int ClosedExceptionCount,
    int WindowMonths,
    DateTimeOffset FirstOccurredAt,
    DateTimeOffset LastOccurredAt,
    DateTimeOffset DetectedAt,
    DateTimeOffset? NotifiedAt,
    IReadOnlyList<RecurrenceClusterMemberDto> Members);

/// <summary>One member exception inside a recurrence cluster.</summary>
public sealed record RecurrenceClusterMemberDto(
    Guid ExceptionId,
    Guid AuditId,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ClosedAt);
