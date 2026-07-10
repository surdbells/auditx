using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Analytics.Dtos;
using AuditX.Application.Common.Enums;
using AuditX.Domain.Analytics;

namespace AuditX.Application.Analytics.Mapping;

public static class AnalyticsMappings
{
    public static DashboardListItemDto ToListDto(this Dashboard d) => new(
        d.Id, d.Slug, d.Name, d.Description, d.PermissionRequired, d.ConfigurationVersion, d.Widgets.Count);

    /// <summary>Map a widget to its DTO. <paramref name="data"/> is the computed KPI payload (null if unresolved).</summary>
    public static DashboardWidgetDto ToDto(this DashboardWidget w, object? data) => new(
        w.Id,
        w.WidgetType.ToSnake(),
        w.MetricKey,
        w.Title,
        w.TargetRoleId,
        w.Position,
        w.ConfigJson,
        data,
        RowVersionToken.Encode(w.Version));

    public static DashboardDetailDto ToDetailDto(this Dashboard d, IReadOnlyList<DashboardWidgetDto> widgets) => new(
        d.Id, d.Slug, d.Name, d.Description, d.PermissionRequired, d.ConfigurationVersion, widgets, RowVersionToken.Encode(d.Version));

    public static RecurrenceClusterDto ToDto(this RecurrenceCluster c) => new(
        c.Id, c.AuditableEntityId, c.Category, c.ClosedExceptionCount, c.WindowMonths,
        c.FirstOccurredAt, c.LastOccurredAt, c.DetectedAt, c.NotifiedAt);

    public static RecurrenceClusterDetailDto ToDetailDto(this RecurrenceCluster c, IReadOnlyList<RecurrenceMemberProjection> members) => new(
        c.Id, c.AuditableEntityId, c.Category, c.ClosedExceptionCount, c.WindowMonths,
        c.FirstOccurredAt, c.LastOccurredAt, c.DetectedAt, c.NotifiedAt,
        members.Select(m => new RecurrenceClusterMemberDto(
            m.ExceptionId, m.AuditId, m.Title, m.Severity, m.Status, m.RaisedAt, m.ClosedAt)).ToArray());
}
