using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Universe;
using AuditX.Application.Analytics.Dtos;
using AuditX.Application.Analytics.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;

namespace AuditX.Application.Analytics.Queries;

// ---- List dashboards (in-handler permission filter; [Authorize] only at the controller) ----

public sealed record ListDashboardsQuery : IQuery<IReadOnlyList<DashboardListItemDto>>;

public sealed class ListDashboardsQueryHandler(
    IDashboardRepository dashboards, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListDashboardsQuery, IReadOnlyList<DashboardListItemDto>>
{
    public async Task<IReadOnlyList<DashboardListItemDto>> Handle(ListDashboardsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException();
        }

        var all = await dashboards.ListAsync(cancellationToken);
        var visible = new List<DashboardListItemDto>(all.Count);
        foreach (var dashboard in all)
        {
            // A dashboard is visible when it has no permission gate, or the caller holds that gate.
            if (dashboard.PermissionRequired is null
                || await permissions.HasPermissionAsync(userId, dashboard.PermissionRequired, cancellationToken: cancellationToken))
            {
                visible.Add(dashboard.ToListDto());
            }
        }

        return visible;
    }
}

// ---- Get a dashboard by id-or-slug; assemble + populate widgets; record the refresh ----

public sealed record GetDashboardQuery(string IdOrSlug) : IQuery<DashboardDetailDto>;

public sealed class GetDashboardQueryHandler(
    IDashboardRepository dashboards,
    IUserRoleRepository userRoles,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IDispatcher dispatcher,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : IQueryHandler<GetDashboardQuery, DashboardDetailDto>
{
    public async Task<DashboardDetailDto> Handle(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException();
        }

        var dashboard = Guid.TryParse(query.IdOrSlug, out var id)
            ? await dashboards.GetByIdAsync(id, cancellationToken)
            : await dashboards.GetBySlugAsync(query.IdOrSlug.Trim().ToLowerInvariant(), cancellationToken);

        if (dashboard is null)
        {
            throw new NotFoundException("Dashboard", query.IdOrSlug);
        }

        // Same gate as the list: a permission-gated dashboard requires the caller to hold the permission.
        if (dashboard.PermissionRequired is not null
            && !await permissions.HasPermissionAsync(userId, dashboard.PermissionRequired, cancellationToken: cancellationToken))
        {
            throw new ForbiddenAccessException();
        }

        // Widgets the caller sees: bank-wide (TargetRoleId == null) ∪ those targeting one of the caller's roles.
        var roleIds = (await userRoles.GetForUserAsync(userId, cancellationToken))
            .Where(r => r.IsActive)
            .Select(r => r.RoleId)
            .ToHashSet();

        var visibleWidgets = dashboard.Widgets
            .Where(w => w.TargetRoleId is null || roleIds.Contains(w.TargetRoleId.Value))
            .OrderBy(w => w.Position)
            .ThenBy(w => w.Id)
            .ToArray();

        var widgetDtos = new List<DashboardWidgetDto>(visibleWidgets.Length);
        foreach (var widget in visibleWidgets)
        {
            // Isolate each widget: a permission-gated metric (e.g. performance scorecards) the caller cannot see
            // degrades to an empty shell rather than 403-ing the whole dashboard. Other failures still propagate.
            object? data;
            try
            {
                data = await ResolveWidgetDataAsync(widget.MetricKey, cancellationToken);
            }
            catch (ForbiddenAccessException)
            {
                data = null;
            }

            widgetDtos.Add(widget.ToDto(data));
        }

        audit.Record(AuditEventTypes.DashboardRefreshed, AuditTargetTypes.Dashboard, dashboard.Id,
            payload: new { dashboard.Slug, widgetCount = widgetDtos.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return dashboard.ToDetailDto(widgetDtos);
    }

    /// <summary>
    /// Dispatch the KPI query matching a widget's metric key. The sanctions widget is safe to populate inline:
    /// the sanctions-consistency projection physically omits the subject id (FR-M7-010 / NFR-SEC-007).
    /// Unknown keys resolve to null data (the layout still renders the widget shell).
    /// </summary>
    private async Task<object?> ResolveWidgetDataAsync(string metricKey, CancellationToken cancellationToken)
        => metricKey switch
        {
            DashboardMetricKeys.FunctionPerformance => await dispatcher.Query(new FunctionPerformanceQuery(), cancellationToken),
            DashboardMetricKeys.ExceptionPortfolio => await dispatcher.Query(new ExceptionPortfolioQuery(), cancellationToken),
            DashboardMetricKeys.SanctionsConsistency => await dispatcher.Query(new SanctionsConsistencyQuery(), cancellationToken),
            DashboardMetricKeys.MaterialFindings => await dispatcher.Query(new MaterialFindingsQuery(), cancellationToken),
            DashboardMetricKeys.PlanStatus => await dispatcher.Query(new PlanStatusQuery(), cancellationToken),
            DashboardMetricKeys.Coverage => await dispatcher.Query(new AnalyticsCoverageQuery(12), cancellationToken),
            DashboardMetricKeys.RecurrenceClusters => await dispatcher.Query(new GetRecurrenceClustersQuery(null, 20), cancellationToken),
            // Performance scorecards are PerformanceAnalyticsView-gated + self-suppressed inside their handler.
            DashboardMetricKeys.PerformanceScorecards => await dispatcher.Query(new PerformanceScorecardsQuery(), cancellationToken),
            _ => null,
        };
}

/// <summary>The metric keys widgets reference; each maps to a dispatched KPI query in the dashboard detail handler.</summary>
public static class DashboardMetricKeys
{
    public const string FunctionPerformance = "function_performance";
    public const string ExceptionPortfolio = "exception_portfolio";
    public const string SanctionsConsistency = "sanctions_consistency";
    public const string MaterialFindings = "material_findings";
    public const string PlanStatus = "plan_status";
    public const string Coverage = "coverage";
    public const string RecurrenceClusters = "recurrence_clusters";
    public const string PerformanceScorecards = "performance_scorecards";
}
