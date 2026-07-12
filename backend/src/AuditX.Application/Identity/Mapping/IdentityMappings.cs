using AuditX.Application.Common.Enums;
using AuditX.Application.Identity.Dtos;
using AuditX.Domain.Authorization;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.Mapping;

/// <summary>
/// Explicit entity → DTO mappers. We map by hand rather than using a reflection-based mapper
/// (see ADR-0001): the mappings are trivial, allocation-light, AOT-friendly, and free of licensing
/// concerns.
/// </summary>
public static class IdentityMappings
{
    public static UserDto ToDto(this User user) => user.ToDto([]);

    public static UserDto ToDto(this User user, IReadOnlyList<string> roleNames) => new(
        user.Id, user.Email, user.FirstName, user.LastName, user.DisplayName,
        user.Status.ToSnake(), user.LastLoginAt, roleNames);

    public static RoleDto ToDto(this Role role) => new(
        role.Id,
        role.Name,
        role.Description,
        role.IsBuiltIn,
        role.IsArchived,
        role.ParentRoleIds.ToArray(),
        role.Permissions
            .Select(p => new RolePermissionDto(p.PermissionKey, p.ScopeType.ToString(), p.ScopePredicateJson))
            .ToArray());

    public static PermissionDto ToDto(this PermissionDefinition definition) => new(
        definition.Key, definition.Label, definition.Description, definition.Module, definition.FinestScope.ToString());

    public static MakerCheckerActionDto ToDto(this MakerCheckerAction action) => new(
        action.Id,
        action.ActionType,
        action.TargetObjectType,
        action.TargetObjectId,
        action.MakerUserId,
        action.Status.ToSnake(),
        action.CreatedAt,
        action.ResolvedAt,
        action.ResolutionComment);
}
