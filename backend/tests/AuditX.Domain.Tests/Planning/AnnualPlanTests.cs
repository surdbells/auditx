using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Planning;

namespace AuditX.Domain.Tests.Planning;

public sealed class AnnualPlanTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Start = new(2027, 1, 1);
    private static readonly DateOnly End = new(2027, 12, 31);

    private static AnnualPlan NewPlan() => AnnualPlan.Create("FY2027", Start, End);

    private static AnnualPlan PlanWithItem()
    {
        var plan = NewPlan();
        plan.AddItem(Guid.NewGuid(), "branch_operational", new(2027, 2, 1), new(2027, 2, 28), 5m, null);
        return plan;
    }

    [Fact]
    public void AddItem_assigns_incremental_order_and_reorder_sets_by_position()
    {
        var plan = NewPlan();
        var a = plan.AddItem(Guid.NewGuid(), "a", new(2027, 2, 1), new(2027, 2, 28), null, null);
        var b = plan.AddItem(Guid.NewGuid(), "b", new(2027, 3, 1), new(2027, 3, 28), null, null);
        var c = plan.AddItem(Guid.NewGuid(), "c", new(2027, 4, 1), new(2027, 4, 28), null, null);
        Assert.Equal(0, a.OrderIndex);
        Assert.Equal(1, b.OrderIndex);
        Assert.Equal(2, c.OrderIndex);

        plan.ReorderItems([c.Id, a.Id, b.Id]);

        Assert.Equal(0, c.OrderIndex);
        Assert.Equal(1, a.OrderIndex);
        Assert.Equal(2, b.OrderIndex);
    }

    [Fact]
    public void ReorderItems_must_list_every_item_once()
    {
        var plan = NewPlan();
        var a = plan.AddItem(Guid.NewGuid(), "a", new(2027, 2, 1), new(2027, 2, 28), null, null);
        plan.AddItem(Guid.NewGuid(), "b", new(2027, 3, 1), new(2027, 3, 28), null, null);
        Assert.Throws<DomainException>(() => plan.ReorderItems([a.Id]));
    }

    [Fact]
    public void ReorderItems_rejected_once_plan_is_approved()
    {
        var plan = PlanWithItem();
        var item = plan.Items[0];
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, "ok", [], Guid.NewGuid(), Now);
        Assert.Throws<InvalidStateTransitionException>(() => plan.ReorderItems([item.Id]));
    }

    [Fact]
    public void Create_rejects_inverted_period()
        => Assert.Throws<DomainException>(() => AnnualPlan.Create("X", End, Start));

    [Fact]
    public void Submit_requires_at_least_one_item()
    {
        var plan = NewPlan();
        var ex = Assert.Throws<DomainException>(() => plan.Submit(Now));
        Assert.Equal("plan.no_items", ex.Code);
    }

    [Fact]
    public void Item_must_fall_within_plan_period()
    {
        var plan = NewPlan();
        Assert.Throws<DomainException>(() => plan.AddItem(Guid.NewGuid(), "t", new(2026, 12, 1), new(2027, 1, 5), null, null));
    }

    [Fact]
    public void Item_end_before_start_is_rejected()
    {
        var plan = NewPlan();
        Assert.Throws<DomainException>(() => plan.AddItem(Guid.NewGuid(), "t", new(2027, 3, 10), new(2027, 3, 1), null, null));
    }

    [Fact]
    public void Submit_then_approve_transitions_status()
    {
        var plan = PlanWithItem();
        plan.Submit(Now);
        Assert.Equal(PlanStatus.Submitted, plan.Status);

        plan.RecordDecision(AcDecisionOutcome.Approved, "ok", [], Guid.NewGuid(), Now);
        Assert.Equal(PlanStatus.Approved, plan.Status);
        Assert.NotNull(plan.ApprovedAt);
        Assert.NotNull(plan.ApprovalDecision);
    }

    [Fact]
    public void Decision_requires_submitted_state()
    {
        var plan = PlanWithItem(); // Draft
        Assert.Throws<InvalidStateTransitionException>(() => plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now));
    }

    [Fact]
    public void Revisions_requested_reopens_for_editing()
    {
        var plan = PlanWithItem();
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.RevisionsRequested, "tighten scope", [], Guid.NewGuid(), Now);
        Assert.Equal(PlanStatus.RevisionsRequested, plan.Status);

        // editing allowed again
        plan.AddItem(Guid.NewGuid(), "t", new(2027, 4, 1), new(2027, 4, 30), null, null);
        Assert.Equal(2, plan.Items.Count);
    }

    [Fact]
    public void Rejected_decision_preserves_status()
    {
        var plan = PlanWithItem();
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Rejected, "no", [], Guid.NewGuid(), Now);
        Assert.Equal(PlanStatus.Submitted, plan.Status);
        Assert.Equal("Rejected", plan.ApprovalDecision!.Decision);
    }

    [Fact]
    public void Items_cannot_be_removed_once_approved()
    {
        var plan = PlanWithItem();
        var itemId = plan.Items[0].Id;
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);

        Assert.Throws<InvalidStateTransitionException>(() => plan.RemoveItem(itemId));
    }

    [Fact]
    public void Minor_revision_edits_item_dates_while_approved()
    {
        var plan = PlanWithItem();
        var itemId = plan.Items[0].Id;
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);

        plan.ApplyMinorItemDateChange(itemId, new(2027, 5, 1), new(2027, 5, 20));
        Assert.Equal(PlanStatus.Approved, plan.Status);
        Assert.Equal(new DateOnly(2027, 5, 20), plan.Items[0].PlannedEndDate);
    }

    [Fact]
    public void Material_revision_reopens_for_reapproval_and_close_only_from_approved()
    {
        var plan = PlanWithItem();
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);

        plan.BeginMaterialRevision(Now);
        Assert.Equal(PlanStatus.RevisionSubmitted, plan.Status);
        Assert.Throws<InvalidStateTransitionException>(() => plan.Close()); // not from RevisionSubmitted

        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);
        plan.Close();
        Assert.Equal(PlanStatus.Closed, plan.Status);
    }

    [Fact]
    public void Link_audit_requires_approved_plan()
    {
        var plan = PlanWithItem();
        var itemId = plan.Items[0].Id;
        Assert.Throws<InvalidStateTransitionException>(() => plan.LinkAuditToItem(itemId, Guid.NewGuid()));

        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);
        plan.LinkAuditToItem(itemId, Guid.NewGuid());
        Assert.Equal(PlanItemStatus.InProgress, plan.Items[0].Status);
    }

    [Fact]
    public void Linking_an_already_linked_item_is_rejected()
    {
        var plan = PlanWithItem();
        var itemId = plan.Items[0].Id;
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);

        plan.LinkAuditToItem(itemId, Guid.NewGuid());
        var ex = Assert.Throws<DomainException>(() => plan.LinkAuditToItem(itemId, Guid.NewGuid()));
        Assert.Equal("plan.item_already_linked", ex.Code);
    }

    [Fact]
    public void Deferring_a_linked_item_clears_the_link_and_allows_relaunch()
    {
        var plan = PlanWithItem();
        var itemId = plan.Items[0].Id;
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);

        plan.LinkAuditToItem(itemId, Guid.NewGuid());
        Assert.NotNull(plan.Items[0].LinkedAuditId);

        // A cancelled audit defers the item and clears its dangling link so it can be re-launched.
        plan.MarkPlanItemDeferred(itemId);
        Assert.Null(plan.Items[0].LinkedAuditId);
        Assert.Equal(PlanItemStatus.Deferred, plan.Items[0].Status);

        var relaunchedAuditId = Guid.NewGuid();
        plan.LinkAuditToItem(itemId, relaunchedAuditId);
        Assert.Equal(relaunchedAuditId, plan.Items[0].LinkedAuditId);
        Assert.Equal(PlanItemStatus.InProgress, plan.Items[0].Status);
    }

    [Fact]
    public void Link_audit_before_approval_allowed_when_opted_in()
    {
        var plan = PlanWithItem(); // Draft
        var itemId = plan.Items[0].Id;
        plan.LinkAuditToItem(itemId, Guid.NewGuid(), allowBeforeApproval: true);
        Assert.Equal(PlanItemStatus.InProgress, plan.Items[0].Status);
    }

    [Fact]
    public void Link_audit_before_approval_still_rejects_a_closed_plan()
    {
        var plan = PlanWithItem();
        var itemId = plan.Items[0].Id;
        plan.Submit(Now);
        plan.RecordDecision(AcDecisionOutcome.Approved, null, [], Guid.NewGuid(), Now);
        plan.Close();
        Assert.Throws<InvalidStateTransitionException>(() => plan.LinkAuditToItem(itemId, Guid.NewGuid(), allowBeforeApproval: true));
    }
}
