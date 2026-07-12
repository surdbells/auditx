namespace AuditX.Application.Identity.Dtos;

/// <summary>Identity of the current session, returned to the SPA (US-M1-010).</summary>
public sealed record SessionDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt);

/// <summary>Result of a successful authentication: the issued token plus the session it represents.</summary>
public sealed record AuthResultDto(
    string Token,
    DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    SessionDto Session);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string DisplayName,
    string Status,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> RoleNames);

/// <summary>Minimal id→name entry for the shared user directory, readable by any authenticated user
/// so user references (owners, leads, authors, recipients) can be shown as names rather than raw ids.</summary>
public sealed record UserDirectoryEntryDto(Guid Id, string DisplayName);

public sealed record UserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string DisplayName,
    string Status,
    DateTimeOffset? LastLoginAt,
    decimal? CapacityDays,
    IReadOnlyList<UserRoleDto> Roles,
    IReadOnlyList<DelegationDto> Delegations);

public sealed record UserRoleDto(
    Guid Id,
    Guid RoleId,
    string RoleName,
    string? ScopeValue,
    bool IsDelegation,
    DateTimeOffset? DelegationStart,
    DateTimeOffset? DelegationEnd,
    bool IsActive);

public sealed record DelegationDto(
    Guid Id,
    Guid ToUserId,
    Guid RoleId,
    string RoleName,
    Guid? FromUserId,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool IsActive);

public sealed record RolePermissionDto(string Key, string ScopeType, string? ScopePredicateJson);

public sealed record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsBuiltIn,
    bool IsArchived,
    IReadOnlyList<Guid> ParentRoleIds,
    IReadOnlyList<RolePermissionDto> Permissions);

public sealed record PermissionDto(string Key, string Label, string Description, string Module, string FinestScope);

public sealed record MakerCheckerActionDto(
    Guid Id,
    string ActionType,
    string TargetObjectType,
    Guid? TargetObjectId,
    Guid MakerUserId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionComment);

/// <summary>Returned (with HTTP 202) when an action is intercepted by a maker-checker gate.</summary>
public sealed record PendingActionDto(Guid PendingActionId);

/// <summary>
/// Outcome of a role mutation: either applied (<see cref="Role"/> set) or intercepted by a
/// maker-checker gate (<see cref="PendingActionId"/> set). The API maps these to 200/201 vs 202.
/// </summary>
public sealed record RoleMutationResult(RoleDto? Role, Guid? PendingActionId)
{
    public bool IsPending => PendingActionId is not null;
}

/// <summary>Maker-checker gate configuration row (US-M1-020).</summary>
public sealed record MakerCheckerGateDto(string ActionType, bool IsEnabled, string? CheckerRoleName, bool AllowMakerAsChecker);
