using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Audits.Commands;
using AuditX.Application.Audits.Queries;
using AuditX.Application.Common.Messaging;
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
        [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAuditsQuery(status, auditType, lead, planItem, cursor, limit), cancellationToken));

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
}
