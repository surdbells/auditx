using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Organization;

public sealed record OrgUnitDto(Guid Id, string Name, string Code, Guid? ParentOrgUnitId, Guid? HeadUserId, bool IsArchived);

public sealed record GetOrgUnitsQuery(bool IncludeArchived) : IQuery<IReadOnlyList<OrgUnitDto>>;

public sealed class GetOrgUnitsQueryHandler(IOrgUnitRepository orgUnits)
    : IQueryHandler<GetOrgUnitsQuery, IReadOnlyList<OrgUnitDto>>
{
    public async Task<IReadOnlyList<OrgUnitDto>> Handle(GetOrgUnitsQuery query, CancellationToken cancellationToken)
    {
        var all = await orgUnits.GetAllAsync(query.IncludeArchived, cancellationToken);
        return all.Select(o => new OrgUnitDto(o.Id, o.Name, o.Code, o.ParentOrgUnitId, o.HeadUserId, o.IsArchived)).ToArray();
    }
}
