namespace AuditX.Api.Contracts;

public sealed record RegisterRiskRequest(
    string Title, string? Description, string Category, Guid OwnerUserId, Guid? AuditableEntityId,
    int InherentLikelihood, int InherentImpact, DateOnly? TargetDate);

public sealed record UpdateRiskRequest(
    string Title, string? Description, string Category, Guid OwnerUserId, Guid? AuditableEntityId,
    int InherentLikelihood, int InherentImpact, int? ResidualLikelihood, int? ResidualImpact,
    string? TreatmentStrategy, string? TreatmentPlan, DateOnly? TargetDate, DateOnly? NextReviewDate, string Version);

public sealed record ChangeRiskStatusRequest(string Status, string? Rationale, string Version);
