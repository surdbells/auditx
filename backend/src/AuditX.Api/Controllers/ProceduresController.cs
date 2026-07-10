using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Commands;
using AuditX.Application.Execution.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>Typed fieldwork procedures (P2-C): sampling / interview / walkthrough per audit. Record=RespondItem, read=ViewAudit.</summary>
[Authorize]
public sealed class ProceduresController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpPost("api/v1/audits/{auditId:guid}/procedures")]
    public async Task<IActionResult> Record(Guid auditId, [FromBody] RecordProcedureRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RecordProcedureCommand(
            auditId, request.Type, request.ChecklistItemId, request.PerformedOn, request.Summary, request.Counterparty,
            request.Population, request.SampleSize, request.ItemsTested, request.ExceptionsFound, request.Method), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("api/v1/audits/{auditId:guid}/procedures")]
    public async Task<IActionResult> List(Guid auditId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAuditProceduresQuery(auditId), cancellationToken));

    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpDelete("api/v1/procedures/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteProcedureCommand(id, version), cancellationToken);
        return NoContent();
    }
}
