using AuditX.Application.Common.Enums;
using AuditX.Application.Planning.Dtos;
using AuditX.Domain.Enums;
using AuditX.Domain.Planning;

namespace AuditX.Application.Planning.Mapping;

/// <summary>Single source of truth for whether a plan's items may be turned into audits (mirrors AnnualPlan.LinkAuditToItem).</summary>
public static class PlanLaunchPolicy
{
    public static bool CanLaunchAudits(PlanStatus status, bool allowBeforeApproval)
        => allowBeforeApproval ? status != PlanStatus.Closed : status == PlanStatus.Approved;
}

public static class PlanningMappings
{
    public static PlanItemDto ToDto(this PlanItem item) => new(
        item.Id, item.EntityId, item.AuditType, item.PlannedStartDate, item.PlannedEndDate,
        item.EstimatedEffortDays, item.AssignedLeadUserId, item.LinkedAuditId, item.Status.ToSnake(), item.OrderIndex);

    public static PlanDto ToDto(this AnnualPlan plan, bool canLaunchAudits)
    {
        var decision = plan.ApprovalDecision is { } d
            ? new ApprovalDecisionDto(d.Decision, d.Detail, d.Comments, d.DecidedBy, d.DecidedAt)
            : null;

        return new PlanDto(
            plan.Id, plan.PeriodLabel, plan.PeriodStart, plan.PeriodEnd, plan.Status.ToSnake(),
            plan.SubmittedAt, plan.ApprovedAt, decision, canLaunchAudits,
            plan.Items.OrderBy(i => i.OrderIndex).Select(i => i.ToDto()).ToArray());
    }

    public static PlanListItemDto ToListItemDto(this AnnualPlan plan) => new(
        plan.Id, plan.PeriodLabel, plan.PeriodStart, plan.PeriodEnd, plan.Status.ToSnake(), plan.Items.Count);
}
