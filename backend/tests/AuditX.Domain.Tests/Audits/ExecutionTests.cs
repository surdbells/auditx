using AuditX.Domain.Audits;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Audits;

public sealed class ExecutionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();
    private static readonly Guid Auditor = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static Audit InProgressAudit(int items = 1)
    {
        var a = Audit.Create("Branch Audit", "branch", new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 10), null, null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddTeamMember(Auditor, TeamRole.Auditor, Lead, Now);
        for (var i = 0; i < items; i++)
        {
            a.AddChecklistItem($"Q{i}", null, ResponseType.PassFailNa, null, true, null);
        }

        a.Plan();
        a.Start();
        return a;
    }

    private static Guid FirstItemId(Audit a) => a.ChecklistItems[0].Id;

    [Fact]
    public void Responses_locked_until_in_progress()
    {
        var a = Audit.Create("X", "t", new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 1), null, null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddChecklistItem("Q", null, ResponseType.PassFailNa, null, true, null);
        Assert.Throws<InvalidStateTransitionException>(() => a.RecordResponse(a.ChecklistItems[0].Id, ResponseVerdict.Pass, "ok", false, Actor, false, Now));
    }

    [Fact]
    public void Fail_and_na_require_a_comment()
    {
        var a = InProgressAudit();
        var item = FirstItemId(a);
        Assert.Throws<DomainException>(() => a.RecordResponse(item, ResponseVerdict.Fail, " ", false, Actor, false, Now));
        Assert.Throws<DomainException>(() => a.RecordResponse(item, ResponseVerdict.Na, null, false, Actor, false, Now));
    }

    private static Audit InProgressAuditWith(ResponseType type)
    {
        var a = Audit.Create("Branch Audit", "branch", new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 10), null, null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddTeamMember(Auditor, TeamRole.Auditor, Lead, Now);
        a.AddChecklistItem("How many exceptions?", null, type, null, true, null);
        a.Plan();
        a.Start();
        return a;
    }

    [Fact]
    public void Value_type_requires_a_value_to_finalise()
    {
        var a = InProgressAuditWith(ResponseType.Numeric);
        var item = FirstItemId(a);
        var ex = Assert.Throws<DomainException>(
            () => a.RecordResponse(item, verdict: null, comment: null, isDraft: false, Actor, false, Now, valueJson: null));
        Assert.Equal("response.value_required", ex.Code);
    }

    [Fact]
    public void Value_type_finalises_with_a_value_and_no_verdict()
    {
        var a = InProgressAuditWith(ResponseType.Numeric);
        var item = FirstItemId(a);
        var result = a.RecordResponse(item, verdict: null, comment: null, isDraft: false, Actor, false, Now, valueJson: "{\"number\":4}");
        Assert.Equal(ChecklistItemState.Responded, a.ChecklistItems[0].ItemState);
        Assert.Null(result.Response.Verdict);
        Assert.Equal("{\"number\":4}", result.Response.ValueJson);
    }

    [Fact]
    public void Response_type_can_change_while_draft()
    {
        var a = Audit.Create("Branch Audit", "branch", new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 10), null, null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddChecklistItem("Q", null, ResponseType.PassFailNa, null, true, null);
        var itemId = a.ChecklistItems[0].Id;
        a.EditChecklistItem(itemId, "Q", null, ResponseType.Rating, "{\"max\":5}", null, true, null);
        Assert.Equal(ResponseType.Rating, a.ChecklistItems[0].ResponseType);
        Assert.Equal("{\"max\":5}", a.ChecklistItems[0].ResponseConfigJson);
    }

    [Fact]
    public void Draft_keeps_item_in_progress_and_is_replaceable()
    {
        var a = InProgressAudit();
        var item = FirstItemId(a);

        var draft = a.RecordResponse(item, null, null, isDraft: true, Actor, false, Now);
        Assert.True(draft.Response.IsDraft);
        Assert.Equal(ChecklistItemState.InProgress, a.ChecklistItems[0].ItemState);
        Assert.Single(a.Responses);

        // Same item updates the one response row (create-or-update).
        a.RecordResponse(item, ResponseVerdict.Pass, "looks fine", isDraft: false, Actor, false, Now);
        Assert.Single(a.Responses);
        Assert.False(a.Responses[0].IsDraft);
        Assert.True(a.Responses[0].ResponseVersion >= 2);
    }

    [Fact]
    public void Final_response_to_last_item_auto_transitions_to_under_review()
    {
        var a = InProgressAudit(items: 1);
        var result = a.RecordResponse(FirstItemId(a), ResponseVerdict.Pass, null, isDraft: false, Actor, false, Now);
        Assert.True(result.AutoTransitioned);
        Assert.Equal(AuditStatus.UnderReview, a.Status);
        Assert.Equal(ChecklistItemState.Responded, a.ChecklistItems[0].ItemState);
    }

    [Fact]
    public void Discard_draft_returns_item_to_not_started()
    {
        var a = InProgressAudit();
        var item = FirstItemId(a);
        a.RecordResponse(item, null, "wip", isDraft: true, Actor, false, Now);
        a.DiscardDraft(item);
        Assert.Empty(a.Responses);
        Assert.Equal(ChecklistItemState.NotStarted, a.ChecklistItems[0].ItemState);
    }

    [Fact]
    public void Discard_rejects_a_finalised_response()
    {
        var a = InProgressAudit(items: 2); // avoid auto-transition
        var item = FirstItemId(a);
        a.RecordResponse(item, ResponseVerdict.Pass, null, isDraft: false, Actor, false, Now);
        Assert.Throws<DomainException>(() => a.DiscardDraft(item));
    }

    [Fact]
    public void Assign_requires_active_team_member()
    {
        var a = InProgressAudit();
        var item = FirstItemId(a);
        Assert.Throws<DomainException>(() => a.AssignItem(item, Guid.NewGuid()));
        a.AssignItem(item, Auditor);
        Assert.Equal(Auditor, a.ChecklistItems[0].AssignedUserId);
    }

    [Fact]
    public void Response_score_is_applied_and_cleared_by_the_next_edit()
    {
        var a = InProgressAudit(items: 2); // avoid auto-transition
        var item = FirstItemId(a);
        a.RecordResponse(item, ResponseVerdict.Pass, null, isDraft: false, Actor, false, Now);

        a.SetResponseScore(item, 100m);
        Assert.Equal(100m, a.Responses[0].Score);

        // A material edit invalidates the previously computed score — the caller must recompute and re-apply it.
        a.RecordResponse(item, ResponseVerdict.Fail, "control missing", isDraft: false, Actor, false, Now);
        Assert.Null(a.Responses[0].Score);
    }

    [Fact]
    public void Scoring_an_unknown_item_is_rejected()
    {
        var a = InProgressAudit();
        Assert.Throws<DomainException>(() => a.SetResponseScore(Guid.NewGuid(), 50m));
    }

    [Fact]
    public void Fail_judgement_only_on_failed_items()
    {
        var a = InProgressAudit(items: 2);
        var item = FirstItemId(a);
        a.RecordResponse(item, ResponseVerdict.Pass, null, isDraft: false, Actor, false, Now);
        Assert.Throws<DomainException>(() => a.RecordFailJudgement(item, "not warranted because compensating control exists", Actor, Now));

        var failItem = a.ChecklistItems[1].Id;
        a.RecordResponse(failItem, ResponseVerdict.Fail, "control missing", isDraft: false, Actor, false, Now);
        a.RecordFailJudgement(failItem, "Accepted: compensating control documented separately.", Actor, Now);
        Assert.NotNull(a.ChecklistItems[1].FailJustification);
    }
}
