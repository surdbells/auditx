using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Risks.Commands;
using AuditX.Application.Risks.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>Enterprise risk register (P1-A). Reads require ViewRisk; every mutation requires ManageRisk.</summary>
[Authorize]
[Route("api/v1/risks")]
public sealed class RisksController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewRisk)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? category, [FromQuery] Guid? owner, [FromQuery] string? band,
        [FromQuery] bool? includeClosed, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListRisksQuery(status, category, owner, band, includeClosed ?? true, search, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewRisk)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetRiskQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRisk)]
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterRiskRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RegisterRiskCommand(
            request.Title, request.Description, request.Category, request.OwnerUserId, request.AuditableEntityId,
            request.InherentLikelihood, request.InherentImpact, request.TargetDate), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRisk)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRiskRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateRiskCommand(
            id, request.Title, request.Description, request.Category, request.OwnerUserId, request.AuditableEntityId,
            request.InherentLikelihood, request.InherentImpact, request.ResidualLikelihood, request.ResidualImpact,
            request.TreatmentStrategy, request.TreatmentPlan, request.TargetDate, request.NextReviewDate, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRisk)]
    [HttpPost("{id:guid}/transition")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] ChangeRiskStatusRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ChangeRiskStatusCommand(id, request.Status, request.Rationale, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRisk)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteRiskCommand(id, version), cancellationToken);
        return NoContent();
    }
}
