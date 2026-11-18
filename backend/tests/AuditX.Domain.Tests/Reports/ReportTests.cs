using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using AuditX.Domain.Reports.Events;

namespace AuditX.Domain.Tests.Reports;

public sealed class ReportTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static readonly Guid Preparer = Guid.NewGuid();

    private static Report NewReport(ReportStage stage = ReportStage.Draft) => Report.Start(
        Guid.NewGuid(), versionNumber: 1, Guid.NewGuid(), templateVersion: 1, "{\"sections\":[]}",
        ["html"], Preparer, Now, stage);

    private static readonly IReadOnlyList<ProducedArtefact> Artefacts =
        [new ProducedArtefact("html", "reports/x/v1/html/a.html", "text/html", 123, "abc123")];

    [Fact]
    public void Start_begins_pending_and_raises_requested_event()
    {
        var r = NewReport();
        Assert.Equal(ReportStatus.Pending, r.Status);
        Assert.Contains(r.DomainEvents, e => e is ReportGenerationRequestedEvent);
    }

    [Fact]
    public void Complete_after_running_marks_completed_and_raises_generated_event()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Complete("hash-1", Artefacts, "[{}]", Now);

        Assert.Equal(ReportStatus.Completed, r.Status);
        Assert.Equal("hash-1", r.Sha256Hash);
        Assert.Contains(r.DomainEvents, e => e is ReportGeneratedEvent);
    }

    [Fact]
    public void Complete_directly_from_pending_is_an_illegal_transition()
    {
        var r = NewReport(); // still pending (no MarkRunning)
        Assert.ThrowsAny<DomainException>(() => r.Complete("h", Artefacts, "[{}]", Now));
    }

    [Fact]
    public void Complete_requires_at_least_one_artefact()
    {
        var r = NewReport();
        r.MarkRunning();
        var ex = Assert.Throws<DomainException>(() => r.Complete("h", [], "[]", Now));
        Assert.Equal("report.no_artefacts", ex.Code);
    }

    [Fact]
    public void Fail_marks_failed_and_raises_event()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Fail("renderer blew up");
        Assert.Equal(ReportStatus.Failed, r.Status);
        Assert.Contains(r.DomainEvents, e => e is ReportGenerationFailedEvent);
    }

    [Fact]
    public void A_non_completed_report_cannot_be_distributed()
    {
        var r = NewReport(); // pending
        Assert.ThrowsAny<DomainException>(() => r.RecordDistribution(
            Guid.NewGuid(), null, "a@b.com", Guid.NewGuid(), Now, "Audit", 10, 2, "1 high, 1 low"));
    }

    [Fact]
    public void RecordDistribution_on_a_completed_report_adds_a_row_and_raises_event()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Complete("h", Artefacts, "[{}]", Now);

        r.RecordDistribution(Guid.NewGuid(), null, "a@b.com", Guid.NewGuid(), Now, "Audit", 10, 2, "summary");

        Assert.Single(r.Distributions);
        Assert.Contains(r.DomainEvents, e => e is ReportDistributedEvent);
    }

    [Fact]
    public void Distribution_requires_exactly_one_of_user_or_email()
    {
        var r = NewReport();
        r.MarkRunning();
        r.Complete("h", Artefacts, "[{}]", Now);

        // neither user nor ad-hoc email set
        Assert.ThrowsAny<DomainException>(() => r.RecordDistribution(
            null, null, "resolved@b.com", Guid.NewGuid(), Now, "Audit", 1, 0, "none"));
    }
}

public sealed class ReportReviewTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Guid Preparer = Guid.NewGuid();
    private static readonly Guid Cae = Guid.NewGuid();

    private static readonly IReadOnlyList<ProducedArtefact> Artefacts =
        [new ProducedArtefact("html", "reports/x/v1/html/a.html", "text/html", 123, "abc123")];

    private static Report Completed(ReportStage stage)
    {
        var r = Report.Start(Guid.NewGuid(), 1, Guid.NewGuid(), 1, "{\"sections\":[]}", ["html"], Preparer, Now, stage);
        r.MarkRunning();
        r.Complete("h", Artefacts, "[{}]", Now);
        return r;
    }

    private static void Distribute(Report r)
        => r.RecordDistribution(Guid.NewGuid(), null, "a@b.com", Preparer, Now, "Audit", 10, 2, "summary");

    [Fact]
    public void A_draft_needs_no_sign_off_and_can_be_sent_for_comment()
    {
        var draft = Completed(ReportStage.Draft);
        Assert.Equal(ReportSignOffStatus.NotRequired, draft.SignOffStatus);
        Assert.True(draft.IsDistributable);
        Distribute(draft);
        Assert.Equal("draft", Assert.IsType<ReportDistributedEvent>(draft.DomainEvents.Last()).Stage);
    }

    [Fact]
    public void A_final_report_awaits_sign_off_and_cannot_go_out_before_it()
    {
        var final = Completed(ReportStage.Final);
        Assert.Equal(ReportSignOffStatus.Awaiting, final.SignOffStatus);
        Assert.Contains(final.DomainEvents, e => e is ReportSignOffRequestedEvent);
        Assert.False(final.IsDistributable);

        var refused = Assert.Throws<InvalidStateTransitionException>(() => Distribute(final));
        Assert.Equal("report.not_signed_off", refused.Code);
    }

    [Fact]
    public void The_preparer_cannot_sign_off_their_own_report()
    {
        var final = Completed(ReportStage.Final);
        var refused = Assert.Throws<DomainException>(() => final.SignOff(Preparer, null, Now));
        Assert.Equal("report.signoff_by_preparer", refused.Code);
        Assert.Equal(ReportSignOffStatus.Awaiting, final.SignOffStatus);
    }

    [Fact]
    public void Once_signed_off_a_final_report_can_be_distributed()
    {
        var final = Completed(ReportStage.Final);
        final.SignOff(Cae, "  Approved for issue.  ", Now);

        Assert.Equal(ReportSignOffStatus.SignedOff, final.SignOffStatus);
        Assert.Equal(Cae, final.SignOffBy);
        Assert.Equal("Approved for issue.", final.SignOffNote);
        Assert.Contains(final.DomainEvents, e => e is ReportSignedOffEvent);
        Distribute(final);
        Assert.Single(final.Distributions);

        // A decision is made once.
        Assert.Throws<InvalidStateTransitionException>(() => final.ReturnForChanges(Cae, "Second thoughts on wording.", Now));
    }

    [Fact]
    public void A_returned_final_report_says_why_and_never_goes_out()
    {
        var final = Completed(ReportStage.Final);
        Assert.Throws<DomainException>(() => final.ReturnForChanges(Cae, "too short", Now));

        final.ReturnForChanges(Cae, "Finding 2 needs management's response first.", Now);
        Assert.Equal(ReportSignOffStatus.Returned, final.SignOffStatus);
        Assert.Equal("Finding 2 needs management's response first.", final.SignOffNote);
        Assert.Contains(final.DomainEvents, e => e is ReportReturnedEvent);
        Assert.Throws<InvalidStateTransitionException>(() => Distribute(final));
        Assert.Throws<InvalidStateTransitionException>(() => final.SignOff(Cae, null, Now));
    }

    [Fact]
    public void Drafts_take_comments_and_finals_do_not()
    {
        var draft = Completed(ReportStage.Draft);
        var comment = draft.AddComment(Cae, "  Please soften the wording of finding 1.  ", Now);
        Assert.Equal("Please soften the wording of finding 1.", comment.Body);
        Assert.Single(draft.Comments);
        Assert.Contains(draft.DomainEvents, e => e is ReportCommentAddedEvent);
        Assert.Throws<DomainException>(() => draft.AddComment(Cae, "   ", Now));

        var final = Completed(ReportStage.Final);
        var refused = Assert.Throws<InvalidStateTransitionException>(() => final.AddComment(Cae, "A comment on the final.", Now));
        Assert.Equal("report.comment_not_draft", refused.Code);
    }

    [Fact]
    public void A_draft_is_never_signed_off()
    {
        var draft = Completed(ReportStage.Draft);
        Assert.Throws<InvalidStateTransitionException>(() => draft.SignOff(Cae, null, Now));
    }
}

public sealed class ReportTemplateTests
{
    [Fact]
    public void Activation_requires_a_reason_of_at_least_twenty_chars()
    {
        var t = ReportTemplate.CreateVersion("Default", "{\"sections\":[]}", 2, Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.ThrowsAny<DomainException>(() => t.Activate("too short", Guid.NewGuid(), DateTimeOffset.UnixEpoch));
        t.Activate("Adopting the FY27 audit report template", Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.True(t.IsActive);
    }
}
