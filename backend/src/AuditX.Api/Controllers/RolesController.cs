using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Roles;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/roles")]
public sealed class RolesController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ManageRoles)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
        => Envelope(await dispatcher.Query(new ListRolesQuery(includeArchived), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRoles)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetRoleQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRoles)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new CreateRoleCommand(request.Name, request.Description, request.Permissions, request.ParentRoleIds), cancellationToken);
        return result.IsPending
            ? Accepted(new PendingActionDto(result.PendingActionId!.Value))
            : Created(result.Role!);
    }

    [RequirePermission(PermissionKeys.ManageRoles)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new UpdateRoleCommand(id, request.Name, request.Description, request.Permissions, request.ParentRoleIds), cancellationToken);
        return result.IsPending
            ? Accepted(new PendingActionDto(result.PendingActionId!.Value))
            : Envelope(result.Role!);
    }

    [RequirePermission(PermissionKeys.ManageRoles)]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ArchiveRoleCommand(id), cancellationToken);
        return NoContent();
    }
}
