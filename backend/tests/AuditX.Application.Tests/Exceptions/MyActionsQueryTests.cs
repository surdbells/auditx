using AuditX.Application.Exceptions.Queries;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Tests.Exceptions;

/// <summary>What one finding puts on each person's "my actions" worklist.</summary>
public sealed class MyActionsQueryTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2027, 1, 1);
    private static readonly DateOnly Target = new(2027, 3, 1);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid ActionOwner = Guid.NewGuid();
    private static readonly Guid Raiser = Guid.NewGuid();
    private static readonly Guid Auditor = Guid.NewGuid();

    private static AuditException New() => AuditException.Raise(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Cash control gap", ExceptionSeverity.High, "rc", "rec", null, null,
        Owner, Raiser, Target, false, null, false, null, "{}", Now);

    private static string[] Kinds(AuditException e, Guid who) => [.. MyActionsQueryHandler.ItemsFor(e, who, Today).Select(i => i.Kind)];

    [Fact]
    public void A_new_finding_asks_its_owner_to_respond_and_write_a_plan()
    {
        var e = New();
        Assert.Equal(["respond", "submit_plan"], Kinds(e, Owner));
        Assert.Empty(Kinds(e, Raiser));
    }

    [Fact]
    public void An_agreed_plan_asks_each_action_owner_for_their_action()
    {
        var e = New();
        e.RecordManagementResponse(ManagementResponseDecision.Accepted, "We agree.", Owner, Now);
        e.AddMapAction("Fix it", ActionOwner, Today.AddDays(-2), null);
        e.SubmitMap(Owner, Now);

        // Nothing for the action owner until the plan is agreed.
        Assert.Empty(Kinds(e, ActionOwner));

        e.ApproveMap(Auditor, Now);
        var item = Assert.Single(MyActionsQueryHandler.ItemsFor(e, ActionOwner, Today));
        Assert.Equal("complete_action", item.Kind);
        Assert.True(item.IsOverdue);
        Assert.Equal(-2, item.DaysUntilDue);
        Assert.Empty(Kinds(e, Owner));
    }

    [Fact]
    public void A_request_for_more_time_flags_the_action_and_asks_the_raiser_to_decide()
    {
        var e = New();
        e.RecordManagementResponse(ManagementResponseDecision.Accepted, "We agree.", Owner, Now);
        var action = e.AddMapAction("Fix it", ActionOwner, Target.AddDays(-10), null);
        e.SubmitMap(Owner, Now);
        e.ApproveMap(Auditor, Now);
        var request = e.RequestExtension(action.Id, Target.AddDays(-1), "The vendor's release slipped by a week.", ActionOwner, Today, Now);

        Assert.True(Assert.Single(MyActionsQueryHandler.ItemsFor(e, ActionOwner, Today)).ExtensionPending);
        var decide = Assert.Single(MyActionsQueryHandler.ItemsFor(e, Raiser, Today));
        Assert.Equal("decide_extension", decide.Kind);
        Assert.Equal(request.Id, decide.ExtensionRequestId);

        e.ApproveExtension(request.Id, Auditor, null, Now);
        Assert.Empty(Kinds(e, Raiser));
    }

    [Fact]
    public void The_raiser_is_not_asked_to_decide_a_request_they_made()
    {
        var e = New();
        e.RequestExtension(null, Target.AddDays(30), "Asked on the business's behalf after a call.", Raiser, Today, Now);
        Assert.DoesNotContain("decide_extension", Kinds(e, Raiser));
    }

    [Fact]
    public void A_finished_plan_asks_the_owner_to_mark_it_complete()
    {
        var e = New();
        e.RecordManagementResponse(ManagementResponseDecision.Accepted, "We agree.", Owner, Now);
        var action = e.AddMapAction("Fix it", ActionOwner, Target, null);
        e.SubmitMap(Owner, Now);
        e.ApproveMap(Auditor, Now);
        e.MarkMapActionComplete(action.Id, true, true, ActionOwner, Now);

        Assert.Equal(["complete_plan"], Kinds(e, Owner));
        Assert.Empty(Kinds(e, ActionOwner));
    }
}
