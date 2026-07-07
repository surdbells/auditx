using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Planning;

/// <summary>A single planned audit within an annual plan (M3). Status is driven by the linked audit (M4).</summary>
public sealed class PlanItem : Entity
{
    private PlanItem()
    {
    }

    public Guid AnnualPlanId { get; private set; }

    public Guid EntityId { get; private set; }

    public string AuditType { get; private set; } = null!;

    public DateOnly PlannedStartDate { get; private set; }

    public DateOnly PlannedEndDate { get; private set; }

    public decimal? EstimatedEffortDays { get; private set; }

    public Guid? AssignedLeadUserId { get; private set; }

    public Guid? LinkedAuditId { get; private set; }

    public PlanItemStatus Status { get; private set; } = PlanItemStatus.Planned;

    /// <summary>Display order within the plan (manually reorderable while the plan is editable).</summary>
    public int OrderIndex { get; private set; }

    internal PlanItem(Guid annualPlanId, Guid entityId, string auditType, DateOnly start, DateOnly end, decimal? effortDays, Guid? assignedLeadUserId, int orderIndex)
    {
        AnnualPlanId = annualPlanId;
        EntityId = entityId;
        AuditType = Guard.NotNullOrWhiteSpace(auditType, "plan.audit_type_required", "Audit type is required.");
        SetDates(start, end);
        EstimatedEffortDays = effortDays;
        AssignedLeadUserId = assignedLeadUserId;
        OrderIndex = orderIndex;
        Status = PlanItemStatus.Planned;
    }

    internal void SetOrder(int orderIndex) => OrderIndex = orderIndex;

    internal void SetDates(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainException("plan.item_date_order", "Planned end date must be on or after the planned start date.");
        }

        PlannedStartDate = start;
        PlannedEndDate = end;
    }

    internal void LinkAudit(Guid auditId)
    {
        LinkedAuditId = auditId;
        Status = PlanItemStatus.InProgress;
    }

    internal void MarkCompleted() => Status = PlanItemStatus.Completed;

    internal void MarkDeferred() => Status = PlanItemStatus.Deferred;
}
