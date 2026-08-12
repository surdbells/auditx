using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Planning;

/// <summary>
/// A single scheduled slot within an annual plan (M3): an audit type + date range + lead, covering one or more
/// auditable entities. Each entity gets its own <see cref="PlanItemEntityLink"/> and, once launched, its own
/// independent audit — the item is the shared schedule, not a 1:1 audit template.
/// </summary>
public sealed class PlanItem : Entity
{
    private readonly List<PlanItemEntityLink> _entityLinks = [];

    private PlanItem()
    {
    }

    public Guid AnnualPlanId { get; private set; }

    public string AuditType { get; private set; } = null!;

    public DateOnly PlannedStartDate { get; private set; }

    public DateOnly PlannedEndDate { get; private set; }

    public decimal? EstimatedEffortDays { get; private set; }

    public Guid? AssignedLeadUserId { get; private set; }

    /// <summary>Display order within the plan (manually reorderable while the plan is editable).</summary>
    public int OrderIndex { get; private set; }

    public IReadOnlyList<PlanItemEntityLink> EntityLinks => _entityLinks.AsReadOnly();

    /// <summary>Roll-up for list display: InProgress if any entity is; else Completed only if every entity is; else Planned.</summary>
    public PlanItemStatus Status => ComputeRollupStatus(_entityLinks.Select(l => l.Status));

    /// <summary>
    /// The same roll-up <see cref="Status"/> computes, exposed for callers (e.g. analytics) that can only fetch
    /// entity-link statuses via a translated query and must roll them up client-side.
    /// </summary>
    public static PlanItemStatus ComputeRollupStatus(IEnumerable<PlanItemStatus> entityLinkStatuses)
    {
        var statuses = entityLinkStatuses as IReadOnlyCollection<PlanItemStatus> ?? entityLinkStatuses.ToList();
        if (statuses.Any(s => s == PlanItemStatus.InProgress))
        {
            return PlanItemStatus.InProgress;
        }
        if (statuses.Count > 0 && statuses.All(s => s == PlanItemStatus.Completed))
        {
            return PlanItemStatus.Completed;
        }
        if (statuses.All(s => s is PlanItemStatus.Completed or PlanItemStatus.Deferred) && statuses.Any(s => s == PlanItemStatus.Deferred))
        {
            return PlanItemStatus.Deferred;
        }
        return PlanItemStatus.Planned;
    }

    internal PlanItem(Guid annualPlanId, IReadOnlyList<Guid> entityIds, string auditType, DateOnly start, DateOnly end, decimal? effortDays, Guid? assignedLeadUserId, int orderIndex)
    {
        var distinctEntityIds = entityIds.Distinct().ToArray();
        Guard.Against(distinctEntityIds.Length == 0, "plan.item_entities_required", "At least one entity is required.");

        AnnualPlanId = annualPlanId;
        AuditType = Guard.NotNullOrWhiteSpace(auditType, "plan.audit_type_required", "Audit type is required.");
        SetDates(start, end);
        EstimatedEffortDays = effortDays;
        AssignedLeadUserId = assignedLeadUserId;
        OrderIndex = orderIndex;

        foreach (var entityId in distinctEntityIds)
        {
            _entityLinks.Add(new PlanItemEntityLink(Id, entityId));
        }
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

    internal void LinkAudit(Guid entityId, Guid auditId) => FindLink(entityId).LinkAudit(auditId);

    internal void MarkCompleted(Guid entityId) => FindLink(entityId).MarkCompleted();

    internal void MarkDeferred(Guid entityId) => FindLink(entityId).MarkDeferred();

    private PlanItemEntityLink FindLink(Guid entityId)
        => _entityLinks.FirstOrDefault(l => l.EntityId == entityId)
            ?? throw new DomainException("plan.item_entity_not_found", "This entity is not part of the plan item.");
}
