using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Commands;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
public sealed class ExceptionsController(IDispatcher dispatcher) : ApiControllerBase
{
    // ---- Raise + per-audit list (audit-scoped routes) ----

    [RequirePermission(PermissionKeys.RaiseException)]
    [HttpPost("api/v1/audits/{auditId:guid}/exceptions")]
    public async Task<IActionResult> Raise(Guid auditId, [FromBody] RaiseExceptionRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RaiseExceptionCommand(
            auditId, request.ChecklistItemId, request.Title, request.Severity, request.RootCause, request.Recommendation,
            request.Category, request.OwnerUserId, request.TargetDateOverride, request.OverrideRationale), cancellationToken));

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/audits/{auditId:guid}/exceptions")]
    public async Task<IActionResult> ListForAudit(Guid auditId, [FromQuery] string? status, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListExceptionsForAuditQuery(auditId, status), cancellationToken));

    // ---- Cross-audit tracker + detail ----

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions")]
    public async Task<IActionResult> Search(
        [FromQuery] string? status, [FromQuery] string? severity, [FromQuery] Guid? owner, [FromQuery] Guid? entity,
        [FromQuery] Guid? audit, [FromQuery] string? category, [FromQuery] bool? recurrence, [FromQuery] bool? overdue,
        [FromQuery] string? search, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new SearchExceptionsQuery(status, severity, owner, entity, audit, category, recurrence, overdue, search, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetExceptionByIdQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetExceptionHistoryQuery(id), cancellationToken));

    // ---- Exception mutations ----

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPatch("api/v1/exceptions/{id:guid}/severity")]
    public async Task<IActionResult> ChangeSeverity(Guid id, [FromBody] ChangeSeverityRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ChangeSeverityCommand(id, request.Severity, request.Reason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPatch("api/v1/exceptions/{id:guid}/owner")]
    public async Task<IActionResult> ReassignOwner(Guid id, [FromBody] ReassignOwnerRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReassignExceptionOwnerCommand(id, request.OwnerUserId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.CancelException)]
    [HttpPost("api/v1/exceptions/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ReasonVersionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CancelExceptionCommand(id, request.Reason, request.Version), cancellationToken));

    // ---- MAP ----

    [RequirePermission(PermissionKeys.SubmitMap)]
    [HttpPost("api/v1/exceptions/{id:guid}/map")]
    public async Task<IActionResult> SubmitMap(Guid id, [FromBody] SubmitMapRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SubmitMapCommand(id,
            request.Actions.Select(a => new MapActionInput(a.Description, a.OwnerUserId, a.TargetDate, a.ExpectedEvidenceType)).ToArray(), request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ApproveMap)]
    [HttpPost("api/v1/exceptions/{id:guid}/map/approve")]
    public async Task<IActionResult> ApproveMap(Guid id, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ApproveMapCommand(id, request.Version), cancellationToken);
        return result.PendingActionId is { } pendingId ? Accepted(new { pendingActionId = pendingId }) : Envelope(result.Exception);
    }

    [RequirePermission(PermissionKeys.ApproveMap)]
    [HttpPost("api/v1/exceptions/{id:guid}/map/reject")]
    public async Task<IActionResult> RejectMap(Guid id, [FromBody] ReasonVersionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RejectMapCommand(id, request.Reason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ViewEvidence)]
    [HttpGet("api/v1/exceptions/{id:guid}/map/actions/{actionId:guid}/evidence")]
    public async Task<IActionResult> ListActionEvidence(Guid id, Guid actionId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListMapActionEvidenceQuery(id, actionId), cancellationToken));

    [RequirePermission(PermissionKeys.UploadEvidence)]
    [HttpPost("api/v1/exceptions/{id:guid}/map/actions/{actionId:guid}/evidence")]
    [RequestSizeLimit(6L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> UploadActionEvidence(Guid id, Guid actionId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var result = await dispatcher.Send(new UploadMapActionEvidenceCommand(id, actionId, memory.ToArray(), file.FileName, file.ContentType), cancellationToken);
        return Created(result);
    }

    [RequirePermission(PermissionKeys.SubmitMap)]
    [HttpPatch("api/v1/exceptions/{id:guid}/map/actions/{actionId:guid}")]
    public async Task<IActionResult> CompleteAction(Guid id, Guid actionId, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new MarkMapActionCompleteCommand(id, actionId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.SubmitMap)]
    [HttpPost("api/v1/exceptions/{id:guid}/map/mark-complete")]
    public async Task<IActionResult> MarkMapComplete(Guid id, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new MarkMapCompleteCommand(id, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ApproveMap)]
    [HttpPost("api/v1/exceptions/{id:guid}/return-for-evidence")]
    public async Task<IActionResult> ReturnForEvidence(Guid id, [FromBody] ReasonVersionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReturnForEvidenceCommand(id, request.Reason, request.Version), cancellationToken));

    // ---- Closure ----

    [RequirePermission(PermissionKeys.CloseException)]
    [HttpPost("api/v1/exceptions/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseExceptionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CloseExceptionCommand(id, request.ClosureNote, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.Cia)]
    [HttpPost("api/v1/exceptions/{id:guid}/cia-countersign")]
    public async Task<IActionResult> CiaCountersign(Guid id, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CiaCountersignCommand(id, request.Version), cancellationToken));
}
