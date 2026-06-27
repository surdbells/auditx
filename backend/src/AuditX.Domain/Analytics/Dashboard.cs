using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Analytics;

/// <summary>
/// A configurable analytics dashboard (M9). Aggregate root over its widgets. A dashboard has a stable, unique
/// <see cref="Slug"/> and an optional <see cref="PermissionRequired"/> gate (e.g. the sanctions-consistency
/// dashboard requires CIA; a scorecard dashboard requires PerformanceAnalyticsView). Editing the widget set
/// bumps <see cref="ConfigurationVersion"/> so a client can detect a stale layout. Soft-deletable.
/// </summary>
public sealed class Dashboard : AggregateRoot, ISoftDeletable
{
    private readonly List<DashboardWidget> _widgets = [];

    private Dashboard()
    {
    }

    public string Slug { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Optional global permission key required to see this dashboard; null = visible to anyone authenticated.</summary>
    public string? PermissionRequired { get; private set; }

    public int ConfigurationVersion { get; private set; }

    public bool IsSystemDefault { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<DashboardWidget> Widgets => _widgets.AsReadOnly();

    public static Dashboard Create(string slug, string name, string? description, string? permissionRequired, bool isSystemDefault)
        => new()
        {
            Slug = Guard.NotNullOrWhiteSpace(slug, "dashboard.slug_required", "A dashboard slug is required.").Trim().ToLowerInvariant(),
            Name = Guard.NotNullOrWhiteSpace(name, "dashboard.name_required", "A dashboard name is required."),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            PermissionRequired = string.IsNullOrWhiteSpace(permissionRequired) ? null : permissionRequired.Trim(),
            IsSystemDefault = isSystemDefault,
            ConfigurationVersion = 1,
        };

    /// <summary>Add a widget and bump the configuration version (US-M9 widget CRUD).</summary>
    public DashboardWidget AddWidget(
        WidgetType widgetType, string metricKey, string title, Guid? targetRoleId, int position, string? configJson)
    {
        var widget = new DashboardWidget(Id, widgetType, metricKey, title, targetRoleId, position, configJson);
        _widgets.Add(widget);
        ConfigurationVersion++;
        return widget;
    }

    /// <summary>Edit an existing widget's presentation and bump the configuration version.</summary>
    public void EditWidget(Guid widgetId, WidgetType widgetType, string metricKey, string title, Guid? targetRoleId, int position, string? configJson)
    {
        var widget = FindWidget(widgetId);
        widget.Update(widgetType, metricKey, title, targetRoleId, position, configJson);
        ConfigurationVersion++;
    }

    /// <summary>Remove a widget and bump the configuration version.</summary>
    public void RemoveWidget(Guid widgetId)
    {
        _widgets.Remove(FindWidget(widgetId));
        ConfigurationVersion++;
    }

    void ISoftDeletable.SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        DeletedBy = deletedBy;
    }

    private DashboardWidget FindWidget(Guid widgetId)
        => _widgets.FirstOrDefault(w => w.Id == widgetId)
            ?? throw new DomainException("dashboard.widget_not_found", "Dashboard widget not found.");
}
