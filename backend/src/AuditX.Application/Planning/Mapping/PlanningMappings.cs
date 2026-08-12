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

/// <summary>
/// Single source of truth for whether an Approved plan may still be edited directly via a "minor revision"
/// (mirrors the gate in SubmitPlanRevisionCommandHandler). An Approved plan is otherwise fully locked — the
/// only route to change it is a "material revision", which always re-opens it for Audit-Committee re-approval.
/// </summary>
public static class PlanRevisionPolicy
{
    public static bool CanApplyMinorRevision(PlanStatus status, bool allowMinorRevisionAfterApproval)
        => status == PlanStatus.Approved && allowMinorRevisionAfterApproval;
}

public static class PlanningMappings
{
    public static PlanItemEntityLinkDto ToDto(this PlanItemEntityLink link) => new(
        link.Id, link.EntityId, link.LinkedAuditId, link.Status.ToSnake());

    public static PlanItemDto ToDto(this PlanItem item) => new(
        item.Id, item.AuditType, item.PlannedStartDate, item.PlannedEndDate,
        item.EstimatedEffortDays, item.AssignedLeadUserId, item.Status.ToSnake(), item.OrderIndex,
        item.EntityLinks.Select(l => l.ToDto()).ToArray());

    public static PlanDto ToDto(this AnnualPlan plan, bool canLaunchAudits, bool canApplyMinorRevision)
    {
        var decision = plan.ApprovalDecision is { } d
            ? new ApprovalDecisionDto(d.Decision, d.Detail, d.Comments, d.DecidedBy, d.DecidedAt)
            : null;

        return new PlanDto(
            plan.Id, plan.PeriodLabel, plan.PeriodStart, plan.PeriodEnd, plan.Status.ToSnake(),
            plan.SubmittedAt, plan.ApprovedAt, decision, canLaunchAudits, canApplyMinorRevision, plan.RevisionReason,
            plan.Items.OrderBy(i => i.OrderIndex).Select(i => i.ToDto()).ToArray());
    }

    public static PlanListItemDto ToListItemDto(this AnnualPlan plan) => new(
        plan.Id, plan.PeriodLabel, plan.PeriodStart, plan.PeriodEnd, plan.Status.ToSnake(), plan.Items.Count);
}
