using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity;

/// <summary>
/// Where a role grant applies. A role is granted either for the whole organisation (no scope) or for one org unit, in
/// which case its permissions marked "assigned org unit" reach only that unit and the units below it.
/// </summary>
internal static class RoleGrantScope
{
    /// <summary>
    /// Validates and normalises a grant's scope: empty means the whole organisation; anything else must be an active org
    /// unit, and the role (or a role it inherits from) must have at least one permission limited to the assigned org
    /// unit — otherwise limiting the grant would change nothing and mislead whoever reads it.
    /// </summary>
    public static async Task<string?> NormaliseAsync(
        Role role, string? scopeValue, IOrgUnitRepository orgUnits, IRoleRepository roles, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return null;
        }

        if (!Guid.TryParse(scopeValue.Trim(), out var orgUnitId))
        {
            throw new DomainException("role.scope_not_org_unit",
                "A role can only be limited to an org unit. Choose one from the organisation hierarchy, or grant the role for the whole organisation.");
        }

        var unit = await orgUnits.GetByIdAsync(orgUnitId, cancellationToken) ?? throw new NotFoundException("Org unit", orgUnitId);
        if (unit.IsArchived)
        {
            throw new ConflictException("role.scope_org_unit_inactive", $"{unit.Name} is inactive. Reactivate it, or choose another org unit.");
        }

        if (!await HasOrgUnitLimitedPermissionAsync(role, roles, cancellationToken))
        {
            throw new ConflictException("role.not_org_unit_limited",
                $"Every permission of '{role.Name}' applies to the whole organisation, so limiting it to {unit.Name} would change nothing. "
                + "Grant it for the whole organisation, or set some of its permissions to 'Assigned org unit' in the role editor.");
        }

        return orgUnitId.ToString();
    }

    private static async Task<bool> HasOrgUnitLimitedPermissionAsync(Role role, IRoleRepository roles, CancellationToken cancellationToken)
    {
        if (role.Permissions.Any(p => p.ScopeType == PermissionScopeType.OrgUnit))
        {
            return true;
        }

        // Permissions inherited from parent roles apply at the child's grant scope too.
        var graph = await roles.GetInheritanceGraphAsync(cancellationToken);
        var ancestors = new HashSet<Guid>();
        var stack = new Stack<Guid>(role.ParentRoleIds);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!ancestors.Add(current))
            {
                continue;
            }

            if (graph.TryGetValue(current, out var parents))
            {
                foreach (var parent in parents)
                {
                    stack.Push(parent);
                }
            }
        }

        if (ancestors.Count == 0)
        {
            return false;
        }

        var parentRoles = await roles.GetByIdsAsync(ancestors, cancellationToken);
        return parentRoles.Any(r => r.Permissions.Any(p => p.ScopeType == PermissionScopeType.OrgUnit));
    }
}
