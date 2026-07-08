namespace AuditX.Application.Planning.Dtos;

public sealed record PlanItemDto(
    Guid Id,
    Guid EntityId,
    string AuditType,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    decimal? EstimatedEffortDays,
    Guid? AssignedLeadUserId,
    Guid? LinkedAuditId,
    string Status,
    int OrderIndex);

public sealed record ApprovalDecisionDto(string Decision, string? Detail, IReadOnlyList<string> Comments, Guid DecidedBy, DateTimeOffset DecidedAt);

public sealed record PlanDto(
    Guid Id,
    string PeriodLabel,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    ApprovalDecisionDto? ApprovalDecision,
    IReadOnlyList<PlanItemDto> Items);

public sealed record PlanListItemDto(Guid Id, string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, int ItemCount);

/// <summary>Locates a plan item within its owning plan — lets an audit link back to the annual plan it fulfils.</summary>
public sealed record PlanItemLocatorDto(Guid PlanItemId, Guid PlanId, string PeriodLabel, string ItemStatus);

public sealed record PlanExecutionDto(
    int TotalItems,
    IReadOnlyDictionary<string, int> CountsByStatus,
    decimal PercentComplete,
    IReadOnlyList<PlanItemDto> BehindSchedule);
