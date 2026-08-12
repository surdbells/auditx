namespace AuditX.Api.Contracts;

// Universe
public sealed record CreateEntityRequest(string Name, string EntityType, string? Description, Guid? ParentEntityId, Guid? OwnerUserId, Guid? OrgUnitId = null, int? ExpectedAuditsPerYear = null);

public sealed record UpdateEntityRequest(string Name, string EntityType, string? Description, Guid? OwnerUserId, Guid? ParentEntityId, string Version, Guid? OrgUnitId = null, int? ExpectedAuditsPerYear = null);

public sealed record RiskScoresRequest(IReadOnlyDictionary<string, int>? InherentScores, IReadOnlyDictionary<string, int>? ResidualScores, string Version);

public sealed record BulkImportEntitiesRequest(string CsvContent);

// Risk dimensions
public sealed record CreateRiskDimensionRequest(string Name, decimal Weight, int? ScaleMin, int? ScaleMax, string? ScaleLabelOverridesJson);

public sealed record UpdateRiskDimensionRequest(decimal? Weight, int? ScaleMin, int? ScaleMax, bool? IsActive, string? ScaleLabelOverridesJson);

// Planning
public sealed record CreatePlanRequest(string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record UpdatePlanRequest(string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record AddPlanItemRequest(IReadOnlyList<Guid> EntityIds, string AuditType, DateOnly PlannedStartDate, DateOnly PlannedEndDate, decimal? EstimatedEffortDays, Guid? AssignedLeadUserId);

public sealed record ReorderPlanItemsRequest(IReadOnlyList<Guid> OrderedItemIds);

public sealed record PlanDecisionRequest(string Decision, string? Detail, IReadOnlyList<string>? Comments);

public sealed record SubmitPlanRevisionRequest(string Kind, Guid? ItemId, DateOnly? NewStartDate, DateOnly? NewEndDate, string? Reason = null);
