using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Compliance.Commands;
using AuditX.Application.Compliance.Queries;
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
            request.Category, request.RootCauseCategory, request.OwnerUserId, request.TargetDateOverride, request.OverrideRationale,
            request.NonConformanceCategory), cancellationToken));

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
        [FromQuery] string? search, [FromQuery] Guid? plan, [FromQuery] DateTimeOffset? raisedFrom, [FromQuery] DateTimeOffset? raisedTo,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new SearchExceptionsQuery(
            status, severity, owner, entity, audit, category, recurrence, overdue, search,
            plan, raisedFrom, raisedTo, page, pageSize), cancellationToken));

    /// <summary>Cross-audit finding-register CSV export (same filters as the tracker). The SHA-256 is on the response header.</summary>
    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? status, [FromQuery] string? severity, [FromQuery] Guid? owner, [FromQuery] Guid? entity,
        [FromQuery] Guid? audit, [FromQuery] string? category, [FromQuery] bool? recurrence, [FromQuery] bool? overdue,
        [FromQuery] string? search, [FromQuery] Guid? plan, [FromQuery] DateTimeOffset? raisedFrom, [FromQuery] DateTimeOffset? raisedTo,
        CancellationToken cancellationToken)
    {
        var export = await dispatcher.Query(new ExportFindingRegisterQuery(
            status, severity, owner, entity, audit, category, recurrence, overdue, search, plan, raisedFrom, raisedTo), cancellationToken);
        Response.Headers["X-Content-SHA256"] = export.Sha256;
        Response.Headers["X-Row-Count"] = export.RowCount.ToString();
        return File(export.Content, export.ContentType, export.FileName);
    }

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetExceptionByIdQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetExceptionHistoryQuery(id), cancellationToken));

    // ---- Control / regulation links (P1-B) ----

    /// <summary>The controls + regulations linked to this finding (access-scoped to the finding's audit).</summary>
    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("api/v1/exceptions/{id:guid}/links")]
    public async Task<IActionResult> Links(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListFindingLinksQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost("api/v1/exceptions/{id:guid}/controls")]
    public async Task<IActionResult> LinkControl(Guid id, [FromBody] LinkControlRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new LinkControlToFindingCommand(id, request.ControlId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpDelete("api/v1/exceptions/{id:guid}/controls/{controlId:guid}")]
    public async Task<IActionResult> UnlinkControl(Guid id, Guid controlId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new UnlinkControlFromFindingCommand(id, controlId), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost("api/v1/exceptions/{id:guid}/regulations")]
    public async Task<IActionResult> LinkRegulation(Guid id, [FromBody] LinkRegulationRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new LinkRegulationToFindingCommand(id, request.RegulationId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpDelete("api/v1/exceptions/{id:guid}/regulations/{regulationId:guid}")]
    public async Task<IActionResult> UnlinkRegulation(Guid id, Guid regulationId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new UnlinkRegulationFromFindingCommand(id, regulationId), cancellationToken);
        return NoContent();
    }

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

    // ---- Management response / follow-up verification / reopen (P2-B) ----

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost("api/v1/exceptions/{id:guid}/management-response")]
    public async Task<IActionResult> RecordManagementResponse(Guid id, [FromBody] ManagementResponseRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RecordManagementResponseCommand(id, request.Decision, request.Comment, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPatch("api/v1/exceptions/{id:guid}/response-due-date")]
    public async Task<IActionResult> SetResponseDueDate(Guid id, [FromBody] SetResponseDueDateRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SetManagementResponseDueDateCommand(id, request.DueDate, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.VerifyException)]
    [HttpPost("api/v1/exceptions/{id:guid}/verifications")]
    public async Task<IActionResult> AddVerification(Guid id, [FromBody] AddVerificationRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new AddFindingVerificationCommand(id, request.Result, request.Notes, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ReopenException)]
    [HttpPost("api/v1/exceptions/{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReasonVersionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReopenExceptionCommand(id, request.Reason, request.Version), cancellationToken));

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
