namespace AuditX.Application.Identity.Roles;

/// <summary>A permission grant as supplied by an administrator when composing a role.</summary>
public sealed record RolePermissionInput(string Key, string ScopeType, string? ScopePredicateJson);

/// <summary>Captured intent for creating a role (also the maker-checker payload shape).</summary>
public sealed record CreateRoleData(
    string Name,
    string Description,
    IReadOnlyList<RolePermissionInput> Permissions,
    IReadOnlyList<Guid> ParentRoleIds);

/// <summary>Captured intent for updating a role (also the maker-checker payload shape).</summary>
public sealed record UpdateRoleData(
    Guid RoleId,
    string Name,
    string Description,
    IReadOnlyList<RolePermissionInput> Permissions,
    IReadOnlyList<Guid> ParentRoleIds);

/// <summary>Tagged envelope persisted as the maker-checker pending payload for role changes.</summary>
public sealed record RoleChangePayload(string Operation, CreateRoleData? Create, UpdateRoleData? Update);
