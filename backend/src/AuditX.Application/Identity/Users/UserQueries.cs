using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Mapping;
using AuditX.Domain.Enums;

namespace AuditX.Application.Identity.Users;

/// <summary>Current user's own profile (US-M1; <c>GET /users/me</c>).</summary>
public sealed record GetMeQuery : IQuery<UserDto>;

public sealed class GetMeQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IQueryHandler<GetMeQuery, UserDto>
{
    public async Task<UserDto> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id)
        {
            throw new UnauthorizedException();
        }

        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);
        return user.ToDto();
    }
}

/// <summary>Paginated user search for the admin surface (US-M15-004).</summary>
public sealed record ListUsersQuery(string? Search, string? Role, string? Status, int? Page, int? PageSize)
    : IQuery<PagedResult<UserDto>>;

public sealed class ListUsersQueryHandler(IUserRepository users)
    : IQueryHandler<ListUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        UserStatus? status = query.Status is null
            ? null
            : Enum.TryParse<UserStatus>(query.Status, ignoreCase: true, out var parsed)
                ? parsed
                : throw new ConflictException("invalid_status", $"Unknown user status '{query.Status}'.");

        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await users.SearchAsync(query.Search, query.Role, status, page, cancellationToken);
        return result.Map(u => u.ToDto());
    }
}

/// <summary>Lightweight id→name directory (any authenticated user) for resolving user references in views.</summary>
public sealed record ListUserDirectoryQuery(int? Page, int? PageSize) : IQuery<PagedResult<UserDirectoryEntryDto>>;

public sealed class ListUserDirectoryQueryHandler(IUserRepository users)
    : IQueryHandler<ListUserDirectoryQuery, PagedResult<UserDirectoryEntryDto>>
{
    public async Task<PagedResult<UserDirectoryEntryDto>> Handle(ListUserDirectoryQuery query, CancellationToken cancellationToken)
    {
        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await users.SearchAsync(null, null, null, page, cancellationToken);
        return result.Map(u => new UserDirectoryEntryDto(u.Id, u.DisplayName));
    }
}

/// <summary>Full user detail including roles and delegations (US-M15-005).</summary>
public sealed record GetUserQuery(Guid Id) : IQuery<UserDetailDto>;

public sealed class GetUserQueryHandler(
    IUserRepository users,
    IUserRoleRepository userRoles,
    IRoleRepository roles)
    : IQueryHandler<GetUserQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("User", query.Id);
        var assignments = await userRoles.GetForUserAsync(user.Id, cancellationToken);
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToArray();
        var roleEntities = roleIds.Length == 0
            ? []
            : await roles.GetByIdsAsync(roleIds, cancellationToken);
        var roleNames = roleEntities.ToDictionary(r => r.Id, r => r.Name);

        string NameOf(Guid roleId) => roleNames.TryGetValue(roleId, out var n) ? n : "(unknown)";

        var roleDtos = assignments
            .Where(a => !a.IsDelegation)
            .Select(a => new UserRoleDto(a.Id, a.RoleId, NameOf(a.RoleId), a.ScopeValue, false, null, null, a.IsActive))
            .ToArray();

        var delegationDtos = assignments
            .Where(a => a.IsDelegation)
            .Select(a => new DelegationDto(a.Id, a.UserId, a.RoleId, NameOf(a.RoleId), a.DelegatedFromUserId, a.DelegationStart, a.DelegationEnd, a.IsActive))
            .ToArray();

        return new UserDetailDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.DisplayName, Common.Enums.EnumExtensions.ToSnake(user.Status),
            user.LastLoginAt, roleDtos, delegationDtos);
    }
}
