using AuditX.Api.Authorization;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Organization;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

public sealed record CreateOrgUnitRequest(string Name, string Code, Guid? ParentOrgUnitId);
public sealed record RenameOrgUnitRequest(string Name);
public sealed record ReparentOrgUnitRequest(Guid? ParentOrgUnitId);
public sealed record SetOrgUnitHeadRequest(Guid? HeadUserId);

/// <summary>The organisational hierarchy (org units) — the roll-up dimension for org-based reporting.</summary>
[Authorize]
[Route("api/v1/org-units")]
public sealed class OrgUnitsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewUniverse)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetOrgUnitsQuery(includeArchived), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrgUnitRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateOrgUnitCommand(request.Name, request.Code, request.ParentOrgUnitId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPatch("{id:guid}/rename")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameOrgUnitRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RenameOrgUnitCommand(id, request.Name), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPatch("{id:guid}/parent")]
    public async Task<IActionResult> Reparent(Guid id, [FromBody] ReparentOrgUnitRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReparentOrgUnitCommand(id, request.ParentOrgUnitId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPatch("{id:guid}/head")]
    public async Task<IActionResult> SetHead(Guid id, [FromBody] SetOrgUnitHeadRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SetOrgUnitHeadCommand(id, request.HeadUserId), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SetOrgUnitArchivedCommand(id, true), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUniverse)]
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SetOrgUnitArchivedCommand(id, false), cancellationToken));
}
