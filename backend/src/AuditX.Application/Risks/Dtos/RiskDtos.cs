namespace AuditX.Application.Risks.Dtos;

/// <summary>A full enterprise-risk record.</summary>
public sealed record RiskDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    Guid OwnerUserId,
    Guid? AuditableEntityId,
    int InherentLikelihood,
    int InherentImpact,
    int InherentScore,
    string InherentBand,
    int? ResidualLikelihood,
    int? ResidualImpact,
    int? ResidualScore,
    int CurrentLikelihood,
    int CurrentImpact,
    int CurrentScore,
    string CurrentBand,
    string? TreatmentStrategy,
    string? TreatmentPlan,
    string Status,
    DateOnly? TargetDate,
    DateOnly? NextReviewDate,
    DateTimeOffset IdentifiedAt,
    Guid IdentifiedByUserId,
    string? ClosureRationale,
    string Version,
    // When the record was created — reference labels on it are shown as they read at that time.
    DateTimeOffset? CreatedAt = null);

/// <summary>Lightweight row for the risk-register list.</summary>
public sealed record RiskListItemDto(
    Guid Id,
    string Title,
    string Category,
    Guid OwnerUserId,
    string Status,
    int CurrentLikelihood,
    int CurrentImpact,
    int CurrentScore,
    string CurrentBand,
    string? TreatmentStrategy,
    DateOnly? TargetDate,
    DateOnly? NextReviewDate,
    // When the record was created — reference labels on it are shown as they read at that time.
    DateTimeOffset? CreatedAt = null);
