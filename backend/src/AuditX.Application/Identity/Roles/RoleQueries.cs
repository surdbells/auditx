using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Mapping;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Identity.Roles;

/// <summary>List roles, optionally including archived ones (US-M1).</summary>
public sealed record ListRolesQuery(bool IncludeArchived) : IQuery<IReadOnlyList<RoleDto>>;

public sealed class ListRolesQueryHandler(IRoleRepository roles)
    : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery query, CancellationToken cancellationToken)
    {
        var result = await roles.GetAllAsync(query.IncludeArchived, cancellationToken);
        return result.Select(r => r.ToDto()).ToArray();
    }
}

/// <summary>Retrieve a single role.</summary>
public sealed record GetRoleQuery(Guid Id) : IQuery<RoleDto>;

public sealed class GetRoleQueryHandler(IRoleRepository roles)
    : IQueryHandler<GetRoleQuery, RoleDto>
{
    public async Task<RoleDto> Handle(GetRoleQuery query, CancellationToken cancellationToken)
    {
        var role = await roles.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Role", query.Id);
        return role.ToDto();
    }
}

/// <summary>The full, code-defined permission catalogue (US-M1-014).</summary>
public sealed record GetPermissionCatalogueQuery : IQuery<IReadOnlyList<PermissionDto>>;

public sealed class GetPermissionCatalogueQueryHandler
    : IQueryHandler<GetPermissionCatalogueQuery, IReadOnlyList<PermissionDto>>
{
    public Task<IReadOnlyList<PermissionDto>> Handle(GetPermissionCatalogueQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionDto> result = PermissionCatalogue.All.Select(d => d.ToDto()).ToArray();
        return Task.FromResult(result);
    }
}
