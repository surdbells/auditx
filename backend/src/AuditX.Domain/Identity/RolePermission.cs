using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Identity;

/// <summary>
/// A single permission grant attached to a role. Permissions are scopeable: a grant is either global
/// or narrowed to a resource kind via <see cref="ScopeType"/>, with optional dynamic predicates in
/// <see cref="ScopePredicateJson"/> for <see cref="PermissionScopeType.Relational"/> scopes.
/// </summary>
public sealed class RolePermission : Entity
{
    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public string PermissionKey { get; private set; } = null!;

    public PermissionScopeType ScopeType { get; private set; }

    public string? ScopePredicateJson { get; private set; }

    internal RolePermission(Guid roleId, string permissionKey, PermissionScopeType scopeType, string? scopePredicateJson)
    {
        RoleId = roleId;
        PermissionKey = permissionKey;
        ScopeType = scopeType;
        ScopePredicateJson = scopePredicateJson;
    }
}
