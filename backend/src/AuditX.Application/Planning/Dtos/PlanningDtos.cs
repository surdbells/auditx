namespace AuditX.Application.Planning.Dtos;

public sealed record PlanItemEntityLinkDto(Guid Id, Guid EntityId, Guid? LinkedAuditId, string Status);

public sealed record PlanItemDto(
    Guid Id,
    string AuditType,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    decimal? EstimatedEffortDays,
    Guid? AssignedLeadUserId,
    string Status,
    int OrderIndex,
    IReadOnlyList<PlanItemEntityLinkDto> EntityLinks);

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
    bool CanLaunchAudits,
    bool CanApplyMinorRevision,
    // Why the current material revision was requested (set when a plan is re-opened for AC re-approval).
    string? RevisionReason,
    IReadOnlyList<PlanItemDto> Items);

public sealed record PlanListItemDto(Guid Id, string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, int ItemCount);

/// <summary>Locates a plan item within its owning plan — lets an audit link back to the annual plan it fulfils.</summary>
public sealed record PlanItemLocatorDto(Guid PlanItemId, Guid PlanId, string PeriodLabel, string ItemStatus);

/// <summary>Real checklist-completion progress of the audit launched for one entity of a plan item.</summary>
public sealed record PlanItemProgressDto(
    Guid PlanItemId,
    Guid EntityId,
    Guid? LinkedAuditId,
    string? AuditStatus,
    int TotalChecklistItems,
    int RespondedChecklistItems,
    decimal PercentComplete);

public sealed record PlanExecutionDto(
    int TotalItems,
    IReadOnlyDictionary<string, int> CountsByStatus,
    decimal PercentComplete,
    IReadOnlyList<PlanItemDto> BehindSchedule,
    int TotalChecklistItems,
    int RespondedChecklistItems,
    decimal ChecklistPercentComplete,
    IReadOnlyList<PlanItemProgressDto> ItemProgress);
