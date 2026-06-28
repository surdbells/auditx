using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Audits.Commands;
using AuditX.Application.Audits.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Commands;
using AuditX.Application.Execution.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/audits")]
public sealed class AuditsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewAudits)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? auditType, [FromQuery] Guid? lead, [FromQuery] Guid? planItem,
        [FromQuery] string? search, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAuditsQuery(status, auditType, lead, planItem, search, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudits)]
    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new AuditCountsQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetAuditQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.CreateAudit)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAuditRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateAuditCommand(
            request.Name, request.AuditType, request.StartDate, request.TargetEndDate, request.ScopeDescription,
            request.TemplateId, request.PlanItemId, request.LeadUserId, request.AuditeeUserId, request.TeamMemberUserIds,
            request.BackdatingOverride, request.BackdatingReason), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAuditRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateAuditMetadataCommand(id, request.Name, request.ScopeDescription, request.StartDate, request.TargetEndDate, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPost("{id:guid}/transition")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionAuditRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new TransitionAuditCommand(id, request.TargetState, request.Reason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelAuditRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CancelAuditCommand(id, request.Reason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPost("{id:guid}/team")]
    public async Task<IActionResult> AddTeamMember(Guid id, [FromBody] AddAuditTeamMemberRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new AddAuditTeamMemberCommand(id, request.UserId, request.TeamRole, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/team")]
    public async Task<IActionResult> Team(Guid id, CancellationToken cancellationToken)
    {
        var audit = await dispatcher.Query(new GetAuditQuery(id), cancellationToken);
        return Envelope(audit.TeamMembers);
    }

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpDelete("{id:guid}/team/{membershipId:guid}")]
    public async Task<IActionResult> RemoveTeamMember(Guid id, Guid membershipId, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RemoveAuditTeamMemberCommand(id, membershipId, version), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPatch("{id:guid}/transfer-lead")]
    public async Task<IActionResult> TransferLead(Guid id, [FromBody] TransferAuditLeadRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new TransferAuditLeadCommand(id, request.NewLeadUserId, request.RemoveOutgoing, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/checklist")]
    public async Task<IActionResult> Checklist(Guid id, CancellationToken cancellationToken)
    {
        var audit = await dispatcher.Query(new GetAuditQuery(id), cancellationToken);
        return Envelope(audit.ChecklistItems);
    }

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPost("{id:guid}/checklist/items")]
    public async Task<IActionResult> AddChecklistItem(Guid id, [FromBody] AddAuditChecklistItemRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new AddAuditChecklistItemCommand(id, request.Prompt, request.ReferenceNotes, request.ResponseType, request.SectionName, request.IsRequired, request.AssignedUserId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPatch("{id:guid}/checklist/items/{itemId:guid}")]
    public async Task<IActionResult> EditChecklistItem(Guid id, Guid itemId, [FromBody] EditAuditChecklistItemRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new EditAuditChecklistItemCommand(id, itemId, request.Prompt, request.ReferenceNotes, request.SectionName, request.IsRequired, request.AssignedUserId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpDelete("{id:guid}/checklist/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveChecklistItem(Guid id, Guid itemId, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RemoveAuditChecklistItemCommand(id, itemId, version), cancellationToken);
        return NoContent();
    }

    // ---- M5 execution / fieldwork ----

    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpPost("{id:guid}/items/{itemId:guid}/responses")]
    public async Task<IActionResult> SubmitResponse(Guid id, Guid itemId, [FromBody] SubmitResponseRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SubmitResponseCommand(id, itemId, request.Verdict, request.Comment, request.IsDraft, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/items/{itemId:guid}/responses")]
    public async Task<IActionResult> GetResponse(Guid id, Guid itemId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetChecklistResponseQuery(id, itemId), cancellationToken));

    [RequirePermission(PermissionKeys.RespondItem)]
    [HttpDelete("{id:guid}/items/{itemId:guid}/responses/draft")]
    public async Task<IActionResult> DiscardDraft(Guid id, Guid itemId, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DiscardDraftCommand(id, itemId, version), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/items/{itemId:guid}/responses/history")]
    public async Task<IActionResult> ResponseHistory(Guid id, Guid itemId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetResponseHistoryQuery(id, itemId), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/checklist/progress")]
    public async Task<IActionResult> ChecklistProgress(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetChecklistProgressQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPatch("{id:guid}/items/{itemId:guid}/assignment")]
    public async Task<IActionResult> AssignItem(Guid id, Guid itemId, [FromBody] AssignItemRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new AssignItemCommand(id, itemId, request.AssigneeUserId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPost("{id:guid}/items/bulk-reassign")]
    public async Task<IActionResult> BulkReassign(Guid id, [FromBody] BulkReassignRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new BulkReassignItemsCommand(id,
            request.Assignments.Select(a => new AuditItemAssignment(a.ItemId, a.AssigneeUserId)).ToArray(), request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/review/fail-without-exception")]
    public async Task<IActionResult> FailWithoutException(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListFailWithoutExceptionQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpGet("{id:guid}/review/summary")]
    public async Task<IActionResult> ReviewSummary(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetReviewSummaryQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageAudit)]
    [HttpPost("{id:guid}/items/{itemId:guid}/fail-judgement")]
    public async Task<IActionResult> RecordFailJudgement(Guid id, Guid itemId, [FromBody] FailJudgementRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RecordFailJudgementCommand(id, itemId, request.Justification, request.Version), cancellationToken));
}
