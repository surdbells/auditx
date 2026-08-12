using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Planning;

/// <summary>
/// One entity a <see cref="PlanItem"/> covers, and the (at most one live) audit launched for it. A plan item
/// schedules a single slot (audit type, date range, lead) that can now target several entities at once — each
/// entity gets its own independently-launched, independently-tracked audit sharing that one schedule.
/// </summary>
public sealed class PlanItemEntityLink : Entity
{
    private PlanItemEntityLink()
    {
    }

    public Guid PlanItemId { get; private set; }

    public Guid EntityId { get; private set; }

    public Guid? LinkedAuditId { get; private set; }

    public PlanItemStatus Status { get; private set; } = PlanItemStatus.Planned;

    internal PlanItemEntityLink(Guid planItemId, Guid entityId)
    {
        PlanItemId = planItemId;
        EntityId = entityId;
    }

    internal void LinkAudit(Guid auditId)
    {
        // 1:1 invariant: a link drives exactly one live audit. A deferred link has had its link cleared
        // (see MarkDeferred) so it can be re-launched; any other link that still carries one is protected.
        if (LinkedAuditId is not null)
        {
            throw new DomainException("plan.item_already_linked", "This plan item's entity already has a linked audit.");
        }

        LinkedAuditId = auditId;
        Status = PlanItemStatus.InProgress;
    }

    internal void MarkCompleted() => Status = PlanItemStatus.Completed;

    /// <summary>The linked audit was cancelled: defer the link and clear the dangling reference so it can be re-launched.</summary>
    internal void MarkDeferred()
    {
        LinkedAuditId = null;
        Status = PlanItemStatus.Deferred;
    }
}
