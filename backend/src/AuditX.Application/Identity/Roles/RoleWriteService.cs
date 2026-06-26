using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Identity.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.Roles;

/// <summary>
/// Applies role create/update mutations. Shared by the direct (non-gated) command handlers and by the
/// maker-checker replay executor so a change behaves identically whether or not it passed through a
/// dual-control gate. Does not commit — callers own the unit of work and cache invalidation.
/// </summary>
public sealed class RoleWriteService(IRoleRepository roles, IAuditRecorder audit)
{
    public async Task<Role> ApplyCreateAsync(CreateRoleData data, CancellationToken cancellationToken)
        => await ApplyCreateAsync(data, actorOverride: null, cancellationToken);

    /// <param name="actorOverride">
    /// When set (maker-checker replay), the executed change is attributed to the original maker rather
    /// than the approving checker (US-M1-022).
    /// </param>
    public async Task<Role> ApplyCreateAsync(CreateRoleData data, Guid? actorOverride, CancellationToken cancellationToken)
    {
        if (await roles.GetByNameAsync(data.Name, cancellationToken) is not null)
        {
            throw new ConflictException("role_name_taken", $"A role named '{data.Name}' already exists.");
        }

        var role = Role.CreateCustom(data.Name, data.Description);
        role.SetPermissions(MapPermissions(data.Permissions));

        var parents = data.ParentRoleIds.Distinct().ToArray();
        if (parents.Length > 0)
        {
            await EnsureNoCycleAsync(role.Id, parents, cancellationToken);
            await EnsureParentsExistAsync(parents, cancellationToken);
            role.SetParents(parents);
        }

        roles.Add(role);
        RecordRoleAudit(actorOverride, AuditEventTypes.RoleCreated, role.Id,
            before: null,
            after: new { role.Name, role.Description, permissions = data.Permissions.Select(p => p.Key), parents });
        return role;
    }

    public async Task<Role> ApplyUpdateAsync(UpdateRoleData data, CancellationToken cancellationToken)
        => await ApplyUpdateAsync(data, actorOverride: null, cancellationToken);

    /// <param name="actorOverride">See <see cref="ApplyCreateAsync(CreateRoleData, Guid?, CancellationToken)"/>.</param>
    public async Task<Role> ApplyUpdateAsync(UpdateRoleData data, Guid? actorOverride, CancellationToken cancellationToken)
    {
        var role = await roles.GetByIdAsync(data.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role", data.RoleId);

        if (role.IsBuiltIn)
        {
            throw new ConflictException("role_builtin_immutable", "Built-in roles cannot be modified; clone into a custom role instead.");
        }

        var existingByName = await roles.GetByNameAsync(data.Name, cancellationToken);
        if (existingByName is not null && existingByName.Id != role.Id)
        {
            throw new ConflictException("role_name_taken", $"A role named '{data.Name}' already exists.");
        }

        var before = new { role.Name, role.Description, permissions = role.Permissions.Select(p => p.PermissionKey).ToArray(), parents = role.ParentRoleIds.ToArray() };

        var parents = data.ParentRoleIds.Distinct().ToArray();
        if (parents.Length > 0)
        {
            await EnsureNoCycleAsync(role.Id, parents, cancellationToken);
            await EnsureParentsExistAsync(parents, cancellationToken);
        }

        role.Rename(data.Name, data.Description);
        role.SetPermissions(MapPermissions(data.Permissions));
        role.SetParents(parents);

        RecordRoleAudit(actorOverride, AuditEventTypes.RoleUpdated, role.Id,
            before: before,
            after: new { role.Name, role.Description, permissions = data.Permissions.Select(p => p.Key), parents });
        return role;
    }

    private void RecordRoleAudit(Guid? actorOverride, string eventType, Guid roleId, object? before, object? after)
    {
        if (actorOverride is { } maker)
        {
            audit.RecordAs(ActorType.User, actorSystemLabel: null, actorUserId: maker, eventType, AuditTargetTypes.Role, roleId, before, after);
        }
        else
        {
            audit.Record(eventType, AuditTargetTypes.Role, roleId, before, after);
        }
    }

    private static IEnumerable<(string, PermissionScopeType, string?)> MapPermissions(IEnumerable<RolePermissionInput> permissions)
    {
        foreach (var permission in permissions)
        {
            if (!Enum.TryParse<PermissionScopeType>(permission.ScopeType, ignoreCase: true, out var scopeType))
            {
                throw new ConflictException("invalid_scope_type", $"Unknown scope type '{permission.ScopeType}'.");
            }

            yield return (permission.Key, scopeType, permission.ScopePredicateJson);
        }
    }

    private async Task EnsureNoCycleAsync(Guid roleId, IReadOnlyCollection<Guid> parents, CancellationToken cancellationToken)
    {
        var graph = await roles.GetInheritanceGraphAsync(cancellationToken);
        if (RoleHierarchy.WouldCreateCycle(graph, roleId, parents))
        {
            throw new ConflictException("role_inheritance_cycle", "The requested inheritance would create a cycle.");
        }
    }

    private async Task EnsureParentsExistAsync(IReadOnlyCollection<Guid> parents, CancellationToken cancellationToken)
    {
        var found = await roles.GetByIdsAsync(parents, cancellationToken);
        if (found.Count != parents.Count)
        {
            throw new ConflictException("role_parent_missing", "One or more parent roles do not exist.");
        }
    }
}
