using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Sanctions.Commands;
using AuditX.Application.Sanctions.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
public sealed class SanctionsGridController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewGrid)]
    [HttpGet("api/v1/sanctions/grid")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetActiveGridQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ManageGrid)]
    [HttpPost("api/v1/sanctions/grid")]
    public async Task<IActionResult> Create([FromBody] CreateGridVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new CreateGridVersionCommand(request.GridDefinition), cancellationToken);
        return result.PendingActionId is { } pendingId ? Accepted(new { pendingActionId = pendingId }) : Created(result.GridVersion);
    }

    [RequirePermission(PermissionKeys.ManageGrid)]
    [HttpPost("api/v1/sanctions/grid/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateGridVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ActivateGridVersionCommand(id, request.ActivationReason), cancellationToken);
        return result.PendingActionId is { } pendingId ? Accepted(new { pendingActionId = pendingId }) : Envelope(result.GridVersion);
    }
}
