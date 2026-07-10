using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Universe.Commands;
using AuditX.Application.Universe.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/audit-universe")]
public sealed class AuditUniverseController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewUniverse)]
    [HttpGet("entities")]
    public async Task<IActionResult> ListEntities(
        [FromQuery] string? entityType, [FromQuery] Guid? owner, [FromQuery] bool archived,
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListEntitiesQuery(entityType, owner, archived, search, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewUniverse)]
    [HttpGet("entities/{id:guid}")]
    public async Task<IActionResult> GetEntity(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetEntityQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPost("entities")]
    public async Task<IActionResult> CreateEntity([FromBody] CreateEntityRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateEntityCommand(request.Name, request.EntityType, request.Description, request.ParentEntityId, request.OwnerUserId, request.OrgUnitId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPatch("entities/{id:guid}")]
    public async Task<IActionResult> UpdateEntity(Guid id, [FromBody] UpdateEntityRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateEntityCommand(id, request.Name, request.EntityType, request.Description, request.OwnerUserId, request.ParentEntityId, request.Version, request.OrgUnitId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpDelete("entities/{id:guid}")]
    public async Task<IActionResult> ArchiveEntity(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ArchiveEntityCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ScoreRisk)]
    [HttpPost("entities/{id:guid}/risk-scores")]
    public async Task<IActionResult> ApplyRiskScores(Guid id, [FromBody] RiskScoresRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ApplyRiskScoresCommand(id, request.InherentScores, request.ResidualScores, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ViewUniverse)]
    [HttpGet("entities/{id:guid}/risk-scores/history")]
    public async Task<IActionResult> RiskScoreHistory(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new RiskScoreHistoryQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPost("entities/bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportEntitiesRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new BulkImportEntitiesCommand(request.CsvContent), cancellationToken));

    // Read-only list of active entity types for populating dropdowns. Entity types are managed as reference data
    // (category "entity_type") through the generic reference-data admin screen — there is no bespoke write endpoint.
    [RequirePermission(PermissionKeys.ViewUniverse)]
    [HttpGet("entity-types")]
    public async Task<IActionResult> ListEntityTypes(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListEntityTypesQuery(), cancellationToken));
}

[Authorize]
[Route("api/v1/risk-dimensions")]
public sealed class RiskDimensionsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewUniverse)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? active, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListRiskDimensionsQuery(active), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRiskDimensionRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateRiskDimensionCommand(request.Name, request.Weight, request.ScaleMin, request.ScaleMax, request.ScaleLabelOverridesJson), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRiskDimensionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateRiskDimensionCommand(id, request.Weight, request.ScaleMin, request.ScaleMax, request.IsActive, request.ScaleLabelOverridesJson), cancellationToken));
}
