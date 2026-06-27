using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Analytics.Commands;
using AuditX.Application.Analytics.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// M9 dashboards. Listing and viewing are [Authorize] with an IN-HANDLER permission filter (a dashboard's
/// optional PermissionRequired gates it — e.g. sanctions_consistency requires CIA), so a caller never even
/// learns a gated dashboard exists. Widget CRUD requires ConfigureDashboards and bumps the configuration version.
/// </summary>
[Authorize]
[Route("api/v1/dashboards")]
public sealed class DashboardsController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListDashboardsQuery(), cancellationToken));

    [HttpGet("{idOrSlug}")]
    public async Task<IActionResult> Get(string idOrSlug, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetDashboardQuery(idOrSlug), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureDashboards)]
    [HttpPost("{id:guid}/widgets")]
    public async Task<IActionResult> AddWidget(Guid id, [FromBody] AddDashboardWidgetRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new AddDashboardWidgetCommand(
            id, request.WidgetType, request.MetricKey, request.Title, request.TargetRoleId, request.Position, request.ConfigJson, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureDashboards)]
    [HttpPatch("{id:guid}/widgets/{widgetId:guid}")]
    public async Task<IActionResult> UpdateWidget(Guid id, Guid widgetId, [FromBody] UpdateDashboardWidgetRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateDashboardWidgetCommand(
            id, widgetId, request.WidgetType, request.MetricKey, request.Title, request.TargetRoleId, request.Position, request.ConfigJson, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureDashboards)]
    [HttpDelete("{id:guid}/widgets/{widgetId:guid}")]
    public async Task<IActionResult> RemoveWidget(Guid id, Guid widgetId, [FromBody] RemoveDashboardWidgetRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RemoveDashboardWidgetCommand(id, widgetId, request.Version), cancellationToken));
}
