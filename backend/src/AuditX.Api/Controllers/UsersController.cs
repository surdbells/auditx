using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Delegations;
using AuditX.Application.Identity.Users;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/users")]
public sealed class UsersController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetMeQuery(), cancellationToken));

    [HttpGet("me/notification-preferences")]
    public async Task<IActionResult> GetMyNotificationPreferences(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetMyNotificationPreferencesQuery(), cancellationToken));

    [HttpPatch("me/notification-preferences")]
    public async Task<IActionResult> UpdateMyNotificationPreferences(
        [FromBody] UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new UpdateNotificationPreferencesCommand(request.PreferencesJson), cancellationToken);
        return NoContent();
    }

    // Authenticated-only (no permission gate): a minimal id→name directory so any signed-in user can
    // display user references (owners, leads, authors, recipients) as names instead of raw ids.
    [HttpGet("directory")]
    public async Task<IActionResult> Directory(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListUserDirectoryQuery(page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListUsersQuery(search, role, status, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetUserQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Status, "deactivated", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("unsupported_update", "Only deactivation is supported via this endpoint.");
        }

        await dispatcher.Send(new DeactivateUserCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpPost("{userId:guid}/roles")]
    public async Task<IActionResult> GrantRole(Guid userId, [FromBody] GrantRoleRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new GrantRoleCommand(userId, request.RoleId, request.ScopeValue), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpDelete("{userId:guid}/roles/{userRoleId:guid}")]
    public async Task<IActionResult> RevokeRole(Guid userId, Guid userRoleId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RevokeRoleCommand(userId, userRoleId), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.Delegate)]
    [HttpPost("{userId:guid}/delegations")]
    public async Task<IActionResult> CreateDelegation(Guid userId, [FromBody] CreateDelegationRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(
            new CreateDelegationCommand(userId, request.ToUserId, request.RoleId, request.StartDate, request.EndDate), cancellationToken));

    [RequirePermission(PermissionKeys.Delegate)]
    [HttpDelete("{userId:guid}/delegations/{delegationId:guid}")]
    public async Task<IActionResult> RevokeDelegation(Guid userId, Guid delegationId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RevokeDelegationCommand(delegationId), cancellationToken);
        return NoContent();
    }
}
