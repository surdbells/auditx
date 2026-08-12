using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Compliance.Commands;
using AuditX.Application.Compliance.Queries;
using AuditX.Application.Controls.Commands;
using AuditX.Application.Controls.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>Internal-controls register (P1-B). Reads require ViewControls; every mutation requires ManageControls.</summary>
[Authorize]
[Route("api/v1/controls")]
public sealed class ControlsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewControls)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type, [FromQuery] string? effectiveness, [FromQuery] Guid? owner,
        [FromQuery] bool? includeRetired, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListControlsQuery(type, effectiveness, owner, includeRetired ?? true, search, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewControls)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetControlQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterControlRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RegisterControlCommand(
            request.Code, request.Title, request.Description, request.ControlType, request.Frequency,
            request.OwnerUserId, request.AuditableEntityId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateControlRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateControlCommand(
            id, request.Title, request.Description, request.ControlType, request.Frequency, request.OwnerUserId,
            request.AuditableEntityId, request.Effectiveness, request.LastTestedDate, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetControlStatusRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SetControlStatusCommand(id, request.IsActive, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteControlCommand(id, version), cancellationToken);
        return NoContent();
    }

    // ---- Linked risks (control↔risk many-to-many) ----

    [RequirePermission(PermissionKeys.ViewControls)]
    [HttpGet("{id:guid}/risks")]
    public async Task<IActionResult> ListRisks(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListControlRisksQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPost("{id:guid}/risks")]
    public async Task<IActionResult> LinkRisk(Guid id, [FromBody] LinkRiskToControlRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new LinkRiskToControlCommand(id, request.RiskId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpDelete("{id:guid}/risks/{riskId:guid}")]
    public async Task<IActionResult> UnlinkRisk(Guid id, Guid riskId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new UnlinkRiskFromControlCommand(id, riskId), cancellationToken);
        return NoContent();
    }

    // ---- Effectiveness tests (append-only history; drives the control's current effectiveness) ----

    [RequirePermission(PermissionKeys.ViewControls)]
    [HttpGet("{id:guid}/tests")]
    public async Task<IActionResult> ListTests(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListControlTestsQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPost("{id:guid}/tests")]
    public async Task<IActionResult> RecordTest(Guid id, [FromBody] RecordControlTestRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RecordControlTestCommand(id, request.AuditId, request.ChecklistItemId, request.Result, request.Notes), cancellationToken));
}

/// <summary>Regulation / compliance register (P1-B). Reads require ViewControls; mutations require ManageControls.</summary>
[Authorize]
[Route("api/v1/regulations")]
public sealed class RegulationsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewControls)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category, [FromQuery] bool? includeRetired, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListRegulationsQuery(category, includeRetired ?? true, search, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewControls)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetRegulationQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterRegulationRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RegisterRegulationCommand(
            request.Code, request.Name, request.Authority, request.Description, request.Category), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRegulationRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateRegulationCommand(
            id, request.Name, request.Authority, request.Description, request.Category, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetRegulationStatusRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SetRegulationStatusCommand(id, request.IsActive, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageControls)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteRegulationCommand(id, version), cancellationToken);
        return NoContent();
    }
}
