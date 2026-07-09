using AuditX.Application.Common.Enums;
using AuditX.Application.Risks.Dtos;
using AuditX.Domain.Risks;

namespace AuditX.Application.Risks.Mapping;

public static class RiskMappings
{
    public static RiskDto ToDto(this Risk risk) => new(
        risk.Id,
        risk.Title,
        risk.Description,
        risk.Category,
        risk.OwnerUserId,
        risk.AuditableEntityId,
        risk.InherentLikelihood,
        risk.InherentImpact,
        risk.InherentScore,
        RiskBands.Band(risk.InherentScore).ToSnake(),
        risk.ResidualLikelihood,
        risk.ResidualImpact,
        risk.ResidualScore,
        risk.CurrentLikelihood,
        risk.CurrentImpact,
        risk.CurrentScore,
        RiskBands.Band(risk.CurrentScore).ToSnake(),
        risk.TreatmentStrategy?.ToSnake(),
        risk.TreatmentPlan,
        risk.Status.ToSnake(),
        risk.TargetDate,
        risk.NextReviewDate,
        risk.IdentifiedAt,
        risk.IdentifiedByUserId,
        risk.ClosureRationale,
        Convert.ToBase64String(risk.Version ?? []));

    public static RiskListItemDto ToListItemDto(this Risk risk) => new(
        risk.Id,
        risk.Title,
        risk.Category,
        risk.OwnerUserId,
        risk.Status.ToSnake(),
        risk.CurrentLikelihood,
        risk.CurrentImpact,
        risk.CurrentScore,
        RiskBands.Band(risk.CurrentScore).ToSnake(),
        risk.TreatmentStrategy?.ToSnake(),
        risk.TargetDate,
        risk.NextReviewDate);
}
