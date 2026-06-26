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
public sealed record ListUsersQuery(string? Search, string? Role, string? Status, string? Cursor, int? Limit)
    : IQuery<CursorPage<UserDto>>;

public sealed class ListUsersQueryHandler(IUserRepository users)
    : IQueryHandler<ListUsersQuery, CursorPage<UserDto>>
{
    public async Task<CursorPage<UserDto>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        UserStatus? status = query.Status is null
            ? null
            : Enum.TryParse<UserStatus>(query.Status, ignoreCase: true, out var parsed)
                ? parsed
                : throw new ConflictException("invalid_status", $"Unknown user status '{query.Status}'.");

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await users.SearchAsync(query.Search, query.Role, status, page, cancellationToken);
        return new CursorPage<UserDto>(result.Items.Select(u => u.ToDto()).ToArray(), result.NextCursor, result.HasMore);
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
