using AuditX.Domain.Audits;
using AuditX.Domain.Audits.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Audits;

/// <summary>
/// Working-paper review: the lead and reviewers sign answered items off and raise review notes; the team answers them;
/// a reviewer clears or reopens them. Nobody reviews their own work, the auditee never reviews, and the audit cannot
/// complete until every item is signed off and every note cleared (a self-assessment excepted).
/// </summary>
public sealed class WorkingPaperReviewTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Start = new(2027, 1, 10);
    private static readonly DateOnly End = new(2027, 2, 10);
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();
    private static readonly Guid Fieldworker = Guid.NewGuid();
    private static readonly Guid Reviewer = Guid.NewGuid();
    private static readonly Guid Outsider = Guid.NewGuid();

    /// <summary>An audit in fieldwork with two items, an auditor and a reviewer on the team.</summary>
    private static Audit InFieldwork()
    {
        var a = Audit.Create("Branch Audit", "branch", Start, End, null, null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddTeamMember(Fieldworker, TeamRole.Auditor, Lead, Now);
        a.AddTeamMember(Reviewer, TeamRole.Reviewer, Lead, Now);
        a.AddChecklistItem("Cash counted?", null, ResponseType.PassFailNa, null, true, null);
        a.AddChecklistItem("Vault dual control?", null, ResponseType.PassFailNa, null, true, null);
        a.Plan();
        a.Start();
        return a;
    }

    private static Guid Item(Audit a, int index) => a.ChecklistItems.OrderBy(i => i.OrderIndex).ElementAt(index).Id;

    private static void Answer(Audit a, int index, Guid by)
        => a.RecordResponse(Item(a, index), ResponseVerdict.Pass, null, isDraft: false, by, requireCommentOnPass: false, Now);

    [Fact]
    public void The_lead_and_reviewers_review_but_not_auditors_auditees_or_outsiders()
    {
        var a = InFieldwork();
        Assert.True(a.IsReviewer(Lead));
        Assert.True(a.IsReviewer(Reviewer));
        Assert.False(a.IsReviewer(Fieldworker));
        Assert.False(a.IsReviewer(Auditee));
        Assert.False(a.IsReviewer(Outsider));

        Answer(a, 0, Fieldworker);
        foreach (var notAReviewer in new[] { Fieldworker, Auditee, Outsider })
        {
            var ex = Assert.Throws<DomainException>(() => a.SignOffItem(Item(a, 0), notAReviewer, Now));
            Assert.Equal("audit.not_reviewer", ex.Code);
        }
    }

    [Fact]
    public void A_reviewer_signs_off_an_answered_item_and_an_event_is_raised()
    {
        var a = InFieldwork();
        Answer(a, 0, Fieldworker);
        a.SignOffItem(Item(a, 0), Reviewer, Now);

        var item = a.ChecklistItems.Single(i => i.Id == Item(a, 0));
        Assert.Equal(Reviewer, item.ReviewedBy);
        Assert.Equal(Now, item.ReviewedAt);
        var ev = Assert.Single(a.DomainEvents.OfType<ItemSignedOffEvent>());
        Assert.Equal(Reviewer, ev.ReviewerUserId);
    }

    [Fact]
    public void An_unanswered_or_draft_item_cannot_be_signed_off()
    {
        var a = InFieldwork();
        Assert.Equal("audit.item_not_answered", Assert.Throws<DomainException>(() => a.SignOffItem(Item(a, 0), Lead, Now)).Code);

        a.RecordResponse(Item(a, 0), ResponseVerdict.Pass, null, isDraft: true, Fieldworker, requireCommentOnPass: false, Now);
        Assert.Equal("audit.item_not_answered", Assert.Throws<DomainException>(() => a.SignOffItem(Item(a, 0), Lead, Now)).Code);
    }

    [Fact]
    public void Nobody_signs_off_an_item_they_answered()
    {
        var a = InFieldwork();
        Answer(a, 0, Lead);
        var ex = Assert.Throws<DomainException>(() => a.SignOffItem(Item(a, 0), Lead, Now));
        Assert.Equal("audit.review_own_work", ex.Code);

        a.SignOffItem(Item(a, 0), Reviewer, Now); // a second pair of eyes may
    }

    [Fact]
    public void Changing_an_answer_withdraws_its_sign_off()
    {
        var a = InFieldwork();
        Answer(a, 0, Fieldworker);
        a.SignOffItem(Item(a, 0), Reviewer, Now);

        a.RecordResponse(Item(a, 0), ResponseVerdict.Fail, "Found a gap after all.", isDraft: false, Fieldworker, requireCommentOnPass: false, Now);
        Assert.Null(a.ChecklistItems.Single(i => i.Id == Item(a, 0)).ReviewedBy);
    }

    [Fact]
    public void A_note_on_an_item_withdraws_its_sign_off_and_blocks_it_until_cleared()
    {
        var a = InFieldwork();
        Answer(a, 0, Fieldworker);
        a.SignOffItem(Item(a, 0), Reviewer, Now);

        var note = a.RaiseReviewNote(Item(a, 0), "  Reference the sample selection.  ", Reviewer, Now);
        Assert.Equal("Reference the sample selection.", note.Body);
        Assert.Equal(ReviewNoteStatus.Open, note.Status);
        Assert.Null(a.ChecklistItems.Single(i => i.Id == Item(a, 0)).ReviewedBy);
        var raised = Assert.Single(a.DomainEvents.OfType<ReviewNoteRaisedEvent>());
        Assert.Equal(Fieldworker, raised.PreparerUserId); // the person who answered the item is told
        Assert.Equal("Branch Audit", raised.AuditName);

        Assert.Equal("audit.item_has_open_notes", Assert.Throws<DomainException>(() => a.SignOffItem(Item(a, 0), Reviewer, Now)).Code);

        a.AnswerReviewNote(note.Id, "Sample referenced in WP 3.", Fieldworker, Now);
        Assert.Equal(ReviewNoteStatus.Answered, note.Status);
        Assert.Equal(Reviewer, Assert.Single(a.DomainEvents.OfType<ReviewNoteAnsweredEvent>()).RaisedBy);
        // Answered is still outstanding until a reviewer accepts it.
        Assert.Equal("audit.item_has_open_notes", Assert.Throws<DomainException>(() => a.SignOffItem(Item(a, 0), Reviewer, Now)).Code);

        a.ClearReviewNote(note.Id, Lead, Now);
        Assert.Equal(ReviewNoteStatus.Cleared, note.Status);
        Assert.Equal(Lead, note.ClearedBy);
        a.SignOffItem(Item(a, 0), Reviewer, Now);
    }

    [Fact]
    public void A_reviewer_reopens_an_unsatisfactory_answer_and_the_answerer_is_told()
    {
        var a = InFieldwork();
        Answer(a, 0, Fieldworker);
        var note = a.RaiseReviewNote(Item(a, 0), "Where is the evidence?", Lead, Now);

        Assert.Equal("audit.review_note_not_answered",
            Assert.Throws<InvalidStateTransitionException>(() => a.ReopenReviewNote(note.Id, Lead)).Code);

        a.AnswerReviewNote(note.Id, "Attached.", Fieldworker, Now);
        a.ReopenReviewNote(note.Id, Lead);
        Assert.Equal(ReviewNoteStatus.Open, note.Status);
        Assert.Equal(Fieldworker, Assert.Single(a.DomainEvents.OfType<ReviewNoteReopenedEvent>()).AnsweredBy);

        a.AnswerReviewNote(note.Id, "Attached to the response now.", Fieldworker, Now);
        Assert.Equal("Attached to the response now.", note.Answer);
    }

    [Fact]
    public void Only_the_audit_team_answers_a_note_and_only_an_open_note()
    {
        var a = InFieldwork();
        var note = a.RaiseReviewNote(null, "Scope memo is missing.", Reviewer, Now);
        Assert.Equal(Lead, Assert.Single(a.DomainEvents.OfType<ReviewNoteRaisedEvent>()).PreparerUserId); // audit-level → lead

        Assert.Equal("audit.not_on_team", Assert.Throws<DomainException>(() => a.AnswerReviewNote(note.Id, "x", Auditee, Now)).Code);
        Assert.Equal("audit.not_on_team", Assert.Throws<DomainException>(() => a.AnswerReviewNote(note.Id, "x", Outsider, Now)).Code);

        a.AnswerReviewNote(note.Id, "Memo filed.", Fieldworker, Now);
        Assert.Equal("audit.review_note_not_open",
            Assert.Throws<InvalidStateTransitionException>(() => a.AnswerReviewNote(note.Id, "again", Fieldworker, Now)).Code);

        a.ClearReviewNote(note.Id, Reviewer, Now);
        Assert.Equal("audit.review_note_cleared",
            Assert.Throws<InvalidStateTransitionException>(() => a.ClearReviewNote(note.Id, Reviewer, Now)).Code);
    }

    [Fact]
    public void Only_reviewers_raise_and_clear_notes()
    {
        var a = InFieldwork();
        Assert.Equal("audit.not_reviewer", Assert.Throws<DomainException>(() => a.RaiseReviewNote(null, "x", Fieldworker, Now)).Code);
        Assert.Equal("audit.not_reviewer", Assert.Throws<DomainException>(() => a.RaiseReviewNote(null, "x", Auditee, Now)).Code);

        var note = a.RaiseReviewNote(null, "Scope memo is missing.", Lead, Now);
        Assert.Equal("audit.not_reviewer", Assert.Throws<DomainException>(() => a.ClearReviewNote(note.Id, Fieldworker, Now)).Code);
    }

    [Fact]
    public void A_note_needs_text_within_the_limit_and_must_exist_on_the_audit()
    {
        var a = InFieldwork();
        Assert.ThrowsAny<DomainException>(() => a.RaiseReviewNote(null, "   ", Lead, Now));
        Assert.Equal("audit.review_note_too_long",
            Assert.Throws<DomainException>(() => a.RaiseReviewNote(null, new string('x', ReviewNote.MaxLength + 1), Lead, Now)).Code);
        Assert.Equal("audit.review_note_not_found",
            Assert.Throws<DomainException>(() => a.ClearReviewNote(Guid.NewGuid(), Lead, Now)).Code);
    }

    [Fact]
    public void The_audit_completes_only_when_every_item_is_signed_off_and_every_note_cleared()
    {
        var a = InFieldwork();
        Answer(a, 0, Fieldworker);
        Answer(a, 1, Fieldworker);
        Assert.Equal(AuditStatus.UnderReview, a.Status);

        Assert.Equal("audit.review_incomplete", Assert.Throws<DomainException>(() => a.Complete(End)).Code);

        a.SignOffItem(Item(a, 0), Lead, Now);
        a.SignOffItem(Item(a, 1), Reviewer, Now);
        var note = a.RaiseReviewNote(null, "Summarise the overall conclusion.", Lead, Now);
        Assert.Equal("audit.review_incomplete", Assert.Throws<DomainException>(() => a.Complete(End)).Code);

        a.AnswerReviewNote(note.Id, "Conclusion added to the summary.", Fieldworker, Now);
        Assert.Equal("audit.review_incomplete", Assert.Throws<DomainException>(() => a.Complete(End)).Code);

        a.ClearReviewNote(note.Id, Lead, Now);
        a.Complete(End);
        Assert.Equal(AuditStatus.Completed, a.Status);
    }

    [Fact]
    public void The_review_is_closed_once_the_audit_is_completed()
    {
        var a = InFieldwork();
        Answer(a, 0, Fieldworker);
        Answer(a, 1, Fieldworker);
        a.SignOffItem(Item(a, 0), Lead, Now);
        a.SignOffItem(Item(a, 1), Lead, Now);
        a.Complete(End);

        Assert.Throws<InvalidStateTransitionException>(() => a.RaiseReviewNote(null, "Late note.", Lead, Now));
    }

    [Fact]
    public void A_self_assessment_completes_without_an_independent_review()
    {
        var a = Audit.Create("Self review", "branch", Start, End, null, null, null, null, null, Lead, Lead, null, Lead, Now, isSelfAssessment: true);
        a.AddChecklistItem("Q", null, ResponseType.PassFailNa, null, true, null);
        a.Plan();
        a.Start();
        a.RecordResponse(a.ChecklistItems[0].Id, ResponseVerdict.Pass, null, isDraft: false, Lead, requireCommentOnPass: false, Now);

        a.Complete(End);
        Assert.Equal(AuditStatus.Completed, a.Status);
    }
}
