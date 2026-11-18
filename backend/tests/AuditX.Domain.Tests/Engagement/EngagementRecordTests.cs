using AuditX.Domain.Common;
using AuditX.Domain.Engagement;
using AuditX.Domain.Engagement.Events;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Engagement;

/// <summary>
/// The engagement letter (drafted, issued to the auditee, acknowledged; a change after issue starts a new revision and
/// keeps the old one) and the kickoff and exit meeting records (attendees, minutes, agreed points; the exit meeting
/// names the draft report it discussed).
/// </summary>
public sealed class EngagementRecordTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AuditId = Guid.NewGuid();
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();

    [Fact]
    public void A_letter_is_drafted_issued_and_acknowledged()
    {
        var letter = EngagementLetter.Draft(AuditId, "  Objectives, scope and timing.  ", Lead, Now);
        Assert.Equal(EngagementLetterStatus.Draft, letter.Status);
        Assert.Equal(1, letter.Revision);
        Assert.Equal("Objectives, scope and timing.", letter.Body);

        letter.Issue(Lead, Now, "Treasury", Auditee);
        Assert.Equal(EngagementLetterStatus.Issued, letter.Status);
        Assert.Contains(letter.DomainEvents, e => e is EngagementLetterIssuedEvent i && i.AuditeeUserId == Auditee && i.Revision == 1);
        Assert.Throws<InvalidStateTransitionException>(() => letter.Issue(Lead, Now, "Treasury", Auditee));

        letter.Acknowledge(Auditee, "  Contacts: J. Bello for payments.  ", Now.AddDays(1), "Treasury");
        Assert.Equal(EngagementLetterStatus.Acknowledged, letter.Status);
        Assert.Equal(Auditee, letter.AcknowledgedBy);
        Assert.Equal("Contacts: J. Bello for payments.", letter.AcknowledgementComment);
        Assert.Contains(letter.DomainEvents, e => e is EngagementLetterAcknowledgedEvent a && a.IssuedBy == Lead);
        Assert.Throws<InvalidStateTransitionException>(() => letter.Acknowledge(Auditee, null, Now, "Treasury"));
    }

    [Fact]
    public void A_draft_cannot_be_acknowledged_and_a_letter_needs_text()
    {
        var letter = EngagementLetter.Draft(AuditId, "Scope.", Lead, Now);
        Assert.Throws<InvalidStateTransitionException>(() => letter.Acknowledge(Auditee, null, Now, "Treasury"));

        var empty = Assert.Throws<DomainException>(() => EngagementLetter.Draft(AuditId, "   ", Lead, Now));
        Assert.Equal("engagement_letter.body_required", empty.Code);
        var tooLong = Assert.Throws<DomainException>(() => EngagementLetter.Draft(AuditId, new string('x', EngagementLetter.MaxBodyLength + 1), Lead, Now));
        Assert.Equal("engagement_letter.body_too_long", tooLong.Code);
    }

    [Fact]
    public void Changing_an_issued_letter_starts_the_next_revision_and_keeps_the_acknowledged_one()
    {
        var letter = EngagementLetter.Draft(AuditId, "Revision one.", Lead, Now);
        letter.Issue(Lead, Now, "Treasury", Auditee);
        letter.Acknowledge(Auditee, "Agreed.", Now.AddDays(1), "Treasury");

        var unchanged = Assert.Throws<DomainException>(() => letter.Revise("Revision one.", Lead, Now.AddDays(2)));
        Assert.Equal("engagement_letter.unchanged", unchanged.Code);

        letter.Revise("Revision two: scope now includes FX settlements.", Lead, Now.AddDays(2));
        Assert.Equal(EngagementLetterStatus.Draft, letter.Status);
        Assert.Equal(2, letter.Revision);
        Assert.Null(letter.IssuedAt);
        Assert.Null(letter.AcknowledgedBy);

        var kept = Assert.Single(letter.PreviousRevisions);
        Assert.Equal(1, kept.Revision);
        Assert.Equal("Revision one.", kept.Body);
        Assert.Equal(Auditee, kept.AcknowledgedBy);
        Assert.Equal("Agreed.", kept.AcknowledgementComment);

        // A draft is edited in place: no new revision.
        letter.Revise("Revision two, reworded.", Lead, Now.AddDays(3));
        Assert.Equal(2, letter.Revision);
        Assert.Single(letter.PreviousRevisions);
    }

    [Fact]
    public void A_kickoff_record_lists_who_attended_the_minutes_and_what_was_agreed()
    {
        var meeting = AuditMeeting.Record(
            AuditId, MeetingKind.Kickoff, Now.AddHours(-1), [Lead, Auditee, Lead], ["  J. Bello (Head of Payments) ", "j. bello (head of payments)"],
            "  Walked through scope.  ", ["Documents within five days.", "  "], reportId: null, Lead, Now, "Treasury", Auditee);

        Assert.Equal([Lead, Auditee], meeting.AttendeeUserIds);
        Assert.Equal(["J. Bello (Head of Payments)"], meeting.ExternalAttendees);
        Assert.Equal("Walked through scope.", meeting.Minutes);
        Assert.Equal(["Documents within five days."], meeting.AgreedPoints);
        Assert.Contains(meeting.DomainEvents, e => e is AuditMeetingRecordedEvent r && r.Meeting == "kickoff" && r.AuditeeUserId == Auditee && !r.IsCorrection);
    }

    [Fact]
    public void A_meeting_is_recorded_after_it_is_held_with_someone_there()
    {
        var future = Assert.Throws<DomainException>(() => AuditMeeting.Record(
            AuditId, MeetingKind.Kickoff, Now.AddHours(1), [Lead], [], "Minutes.", [], null, Lead, Now, "Treasury", Auditee));
        Assert.Equal("meeting.held_in_future", future.Code);

        var nobody = Assert.Throws<DomainException>(() => AuditMeeting.Record(
            AuditId, MeetingKind.Kickoff, Now, [], ["  "], "Minutes.", [], null, Lead, Now, "Treasury", Auditee));
        Assert.Equal("meeting.attendees_required", nobody.Code);

        var noMinutes = Assert.Throws<DomainException>(() => AuditMeeting.Record(
            AuditId, MeetingKind.Kickoff, Now, [Lead], [], " ", [], null, Lead, Now, "Treasury", Auditee));
        Assert.Equal("meeting.minutes_required", noMinutes.Code);
    }

    [Fact]
    public void The_exit_meeting_names_its_draft_report_and_the_kickoff_names_none()
    {
        var noReport = Assert.Throws<DomainException>(() => AuditMeeting.Record(
            AuditId, MeetingKind.Exit, Now, [Lead, Auditee], [], "Findings discussed.", [], null, Lead, Now, "Treasury", Auditee));
        Assert.Equal("meeting.draft_report_required", noReport.Code);

        var kickoffWithReport = Assert.Throws<DomainException>(() => AuditMeeting.Record(
            AuditId, MeetingKind.Kickoff, Now, [Lead], [], "Scope.", [], Guid.NewGuid(), Lead, Now, "Treasury", Auditee));
        Assert.Equal("meeting.report_not_applicable", kickoffWithReport.Code);

        var draft = Guid.NewGuid();
        var exit = AuditMeeting.Record(
            AuditId, MeetingKind.Exit, Now, [Lead, Auditee], [], "Findings discussed; management accepts both.",
            ["Action plans due in two weeks."], draft, Lead, Now, "Treasury", Auditee);
        Assert.Equal(draft, exit.ReportId);

        exit.Correct(Now, [Lead, Auditee], ["CFO"], "Findings discussed; management accepts both.", ["Action plans due in two weeks."],
            draft, Lead, Now.AddHours(1), "Treasury", Auditee);
        Assert.Equal(["CFO"], exit.ExternalAttendees);
        Assert.Equal(Now.AddHours(1), exit.LastEditedAt);
        Assert.Contains(exit.DomainEvents, e => e is AuditMeetingRecordedEvent { IsCorrection: true });
    }
}
