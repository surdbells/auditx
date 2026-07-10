using AuditX.Application.Compliance.Dtos;
using AuditX.Domain.Compliance;

namespace AuditX.Application.Compliance.Mapping;

public static class ComplianceMappings
{
    public static RegulationDto ToDto(this Regulation r) => new(
        r.Id, r.Code, r.Name, r.Authority, r.Description, r.Category, r.IsActive,
        RowVersionToken.Encode(r.Version));

    public static RegulationListItemDto ToListItemDto(this Regulation r) => new(
        r.Id, r.Code, r.Name, r.Authority, r.Category, r.IsActive);
}
