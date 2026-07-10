using AuditX.Application.Universe.Dtos;
using AuditX.Domain.Universe;

namespace AuditX.Application.Universe.Mapping;

public static class UniverseMappings
{
    public static EntityDto ToDto(this AuditableEntity entity) => new(
        entity.Id,
        entity.EntityType,
        entity.Name,
        entity.Description,
        entity.ParentEntityId,
        entity.OwnerUserId,
        entity.OrgUnitId,
        entity.InherentScores,
        entity.ResidualScores,
        entity.CompositeInherentScore,
        entity.CompositeResidualScore,
        entity.LastAuditedAt,
        RowVersionToken.Encode(entity.Version));

    public static RiskDimensionDto ToDto(this RiskDimension dimension) => new(
        dimension.Id, dimension.Name, dimension.Weight, dimension.ScaleMin, dimension.ScaleMax, dimension.IsActive, dimension.ScaleLabelOverridesJson);
}
