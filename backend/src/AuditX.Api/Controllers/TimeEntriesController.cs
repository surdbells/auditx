using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.TimeTracking.Commands;
using AuditX.Application.TimeTracking.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>Time/effort capture (P0-B): logging, listing and the budget-vs-actual summary per audit.</summary>
[Authorize]
public sealed class TimeEntriesController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.LogTime)]
    [HttpPost("api/v1/audits/{auditId:guid}/time-entries")]
    public async Task<IActionResult> Log(Guid auditId, [FromBody] LogTimeRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new LogTimeCommand(
            auditId, request.WorkDate, request.Hours, request.Category, request.ChecklistItemId, request.Notes), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("api/v1/audits/{auditId:guid}/time-entries")]
    public async Task<IActionResult> List(Guid auditId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAuditTimeEntriesQuery(auditId), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("api/v1/audits/{auditId:guid}/time-entries/summary")]
    public async Task<IActionResult> Summary(Guid auditId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetAuditTimeSummaryQuery(auditId), cancellationToken));

    [RequirePermission(PermissionKeys.LogTime)]
    [HttpPatch("api/v1/time-entries/{id:guid}")]
    public async Task<IActionResult> Amend(Guid id, [FromBody] AmendTimeEntryRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new AmendTimeEntryCommand(
            id, request.WorkDate, request.Hours, request.Category, request.ChecklistItemId, request.Notes, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.LogTime)]
    [HttpDelete("api/v1/time-entries/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteTimeEntryCommand(id, version), cancellationToken);
        return NoContent();
    }
}
