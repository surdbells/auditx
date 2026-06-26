using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Identity;

/// <summary>
/// An AuditX authorisation role. Roles are maintained inside AuditX, independent of AD groups
/// (BR-M1-004). Four roles are built in and immutable; administrators may create custom roles and
/// arrange them into an acyclic inheritance hierarchy (cycle detection is performed by the
/// application layer, which has the full role graph).
/// </summary>
public sealed class Role : AggregateRoot
{
    private readonly List<RolePermission> _permissions = [];
    private readonly List<Guid> _parentRoleIds = [];

    private Role()
    {
    }

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = string.Empty;

    public bool IsBuiltIn { get; private set; }

    public bool IsArchived { get; private set; }

    public IReadOnlyList<RolePermission> Permissions => _permissions.AsReadOnly();

    /// <summary>Ids of roles this role inherits permissions from (transitive union at evaluation).</summary>
    public IReadOnlyList<Guid> ParentRoleIds => _parentRoleIds.AsReadOnly();

    public static Role CreateCustom(string name, string description)
    {
        return new Role
        {
            Name = Guard.NotNullOrWhiteSpace(name, "role.name_required", "Role name is required."),
            Description = description?.Trim() ?? string.Empty,
            IsBuiltIn = false,
            IsArchived = false,
        };
    }

    public static Role CreateBuiltIn(BuiltInRoleDefinition definition)
    {
        var role = new Role
        {
            Name = definition.Name,
            Description = definition.Description,
            IsBuiltIn = true,
            IsArchived = false,
        };
        role.SetPermissionsInternal(definition.Permissions.Select(
            key => (key, PermissionScopeType.Global, (string?)null)));
        return role;
    }

    public void Rename(string name, string description)
    {
        EnsureMutable();
        Name = Guard.NotNullOrWhiteSpace(name, "role.name_required", "Role name is required.");
        Description = description?.Trim() ?? string.Empty;
    }

    /// <summary>Replace the role's permission set. Rejected on built-in roles (US-M1-011).</summary>
    public void SetPermissions(IEnumerable<(string Key, PermissionScopeType ScopeType, string? ScopePredicateJson)> permissions)
    {
        EnsureMutable();
        SetPermissionsInternal(permissions);
    }

    private void SetPermissionsInternal(IEnumerable<(string Key, PermissionScopeType ScopeType, string? ScopePredicateJson)> permissions)
    {
        _permissions.Clear();
        foreach (var (key, scopeType, predicate) in permissions)
        {
            if (!PermissionCatalogue.IsValidKey(key))
            {
                throw new DomainException("role.unknown_permission", $"Unknown permission key '{key}'.");
            }

            _permissions.Add(new RolePermission(Id, key, scopeType, predicate));
        }
    }

    /// <summary>Set the parent roles this role inherits from. Cycle prevention is enforced by the caller.</summary>
    public void SetParents(IEnumerable<Guid> parentRoleIds)
    {
        EnsureMutable();
        _parentRoleIds.Clear();
        foreach (var id in parentRoleIds.Distinct())
        {
            if (id == Id)
            {
                throw new DomainException("role.self_inheritance", "A role cannot inherit from itself.");
            }

            _parentRoleIds.Add(id);
        }
    }

    public void Archive()
    {
        if (IsBuiltIn)
        {
            throw new DomainException("role.builtin_immutable", "Built-in roles cannot be archived.");
        }

        IsArchived = true;
    }

    public void Unarchive() => IsArchived = false;

    private void EnsureMutable()
    {
        if (IsBuiltIn)
        {
            throw new DomainException("role.builtin_immutable", "Built-in roles cannot be modified; clone into a custom role instead.");
        }
    }
}
