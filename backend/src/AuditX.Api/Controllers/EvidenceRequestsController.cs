using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Evidence.Commands;
using AuditX.Application.Evidence.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>Expected / requested evidence (P2-D): request, receive, waive per audit. Mutate=RespondItem, read=ViewAudit.</summary>
[Authorize]
public sealed class EvidenceRequestsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpPost("api/v1/audits/{auditId:guid}/evidence-requests")]
    public async Task<IActionResult> Create(Guid auditId, [FromBody] RequestEvidenceRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RequestEvidenceCommand(
            auditId, request.ChecklistItemId, request.ExceptionId, request.Purpose, request.Title, request.DocumentType,
            request.RequestedFromUserId, request.DueDate, request.Notes), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("api/v1/audits/{auditId:guid}/evidence-requests")]
    public async Task<IActionResult> List(Guid auditId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAuditEvidenceRequestsQuery(auditId), cancellationToken));

    /// <summary>The current user's (auditee's) own document requests — their upload worklist. Owner-scoped, no permission gate.</summary>
    [HttpGet("api/v1/my/evidence-requests")]
    public async Task<IActionResult> Mine([FromQuery] bool? outstandingOnly, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new MyEvidenceRequestsQuery(outstandingOnly ?? false), cancellationToken));

    /// <summary>The auditee uploads a document against a request. Recipient-or-manager is enforced in the handler.</summary>
    [HttpPost("api/v1/evidence-requests/{id:guid}/files")]
    [RequestSizeLimit(6L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        return Envelope(await dispatcher.Send(new UploadEvidenceRequestFileCommand(id, memory.ToArray(), file.FileName, file.ContentType), cancellationToken));
    }

    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpPost("api/v1/evidence-requests/{id:guid}/received")]
    public async Task<IActionResult> MarkReceived(Guid id, [FromBody] MarkEvidenceReceivedRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new MarkEvidenceReceivedCommand(id, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpPost("api/v1/evidence-requests/{id:guid}/waive")]
    public async Task<IActionResult> Waive(Guid id, [FromBody] WaiveEvidenceRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new WaiveEvidenceCommand(id, request.Reason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpDelete("api/v1/evidence-requests/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteEvidenceRequestCommand(id, version), cancellationToken);
        return NoContent();
    }
}
