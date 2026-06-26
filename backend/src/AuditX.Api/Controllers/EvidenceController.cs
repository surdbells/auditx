using AuditX.Api.Authorization;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Commands;
using AuditX.Application.Execution.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
public sealed class EvidenceController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.UploadEvidence)]
    [HttpPost("api/v1/audits/{auditId:guid}/responses/{responseId:guid}/evidence")]
    [RequestSizeLimit(6L * 1024 * 1024 * 1024)] // hard ceiling above the per-audit aggregate cap; per-file limit enforced in the handler
    public async Task<IActionResult> Upload(Guid auditId, Guid responseId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);

        var result = await dispatcher.Send(
            new UploadEvidenceCommand(auditId, responseId, memory.ToArray(), file.FileName, file.ContentType), cancellationToken);
        return Created(result);
    }

    [RequirePermission(PermissionKeys.ViewEvidence)]
    [HttpGet("api/v1/audits/{auditId:guid}/responses/{responseId:guid}/evidence")]
    public async Task<IActionResult> List(Guid auditId, Guid responseId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListEvidenceForResponseQuery(auditId, responseId), cancellationToken));

    [RequirePermission(PermissionKeys.ViewEvidence)]
    [HttpGet("api/v1/audits/{auditId:guid}/evidence/{id:guid}")]
    public async Task<IActionResult> Download(Guid auditId, Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Query(new DownloadEvidenceQuery(auditId, id), cancellationToken);
        return File(result.Content, result.MimeType, result.FileName);
    }

    [RequirePermission(PermissionKeys.ManageEvidence)]
    [HttpDelete("api/v1/audits/{auditId:guid}/evidence/{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid auditId, Guid id, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new SoftDeleteEvidenceCommand(auditId, id, reason), cancellationToken);
        return NoContent();
    }
}
