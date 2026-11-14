using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Organization;

/// <summary>An org unit. <see cref="IsArchived"/> is shown as "Inactive": kept for history, not offered for new assignments.</summary>
public sealed record OrgUnitDto(Guid Id, string Name, string Code, Guid? ParentOrgUnitId, Guid? HeadUserId, bool IsArchived, string? UnitType = null);

public static class OrgUnitMappings
{
    public static OrgUnitDto ToDto(this Domain.Organization.OrgUnit o)
        => new(o.Id, o.Name, o.Code, o.ParentOrgUnitId, o.HeadUserId, o.IsArchived, o.UnitType);
}

public sealed record GetOrgUnitsQuery(bool IncludeArchived) : IQuery<IReadOnlyList<OrgUnitDto>>;

public sealed class GetOrgUnitsQueryHandler(IOrgUnitRepository orgUnits)
    : IQueryHandler<GetOrgUnitsQuery, IReadOnlyList<OrgUnitDto>>
{
    public async Task<IReadOnlyList<OrgUnitDto>> Handle(GetOrgUnitsQuery query, CancellationToken cancellationToken)
    {
        var all = await orgUnits.GetAllAsync(query.IncludeArchived, cancellationToken);
        return all.Select(o => o.ToDto()).ToArray();
    }
}
