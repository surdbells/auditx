using AuditX.Application.Engagement;
using AuditX.Application.Engagement.Dtos;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Tests.Engagement;

public sealed class EngagementJourneyResolverTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Start = new(2027, 1, 10);
    private static readonly DateOnly End = new(2027, 2, 10);
    private static readonly DateOnly Target = new(2027, 3, 1);
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Auditor = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();

    private static Audit NewAudit() =>
        Audit.Create("Branch Audit", "branch", Start, End, null, null, null, null, null, Manager, Auditee, null, Manager, Now);

    /// <summary>An audit ready to plan (one auditor + one checklist item assigned to that auditor).</summary>
    private static (Audit Audit, Guid ItemId) Ready()
    {
        var a = NewAudit();
        a.AddTeamMember(Auditor, TeamRole.Auditor, Manager, Now);
        var item = a.AddChecklistItem("Cash counted?", null, ResponseType.PassFailNa, null, true, Auditor);
        return (a, item.Id);
    }

    private static Func<string, bool> Grants(params string[] keys)
    {
        var set = keys.ToHashSet(StringComparer.Ordinal);
        return set.Contains;
    }

    private static EngagementJourneyDto Resolve(Audit a, Func<string, bool> can, Guid userId,
        IReadOnlyList<AuditException>? exceptions = null, bool hasCompletedReport = false, bool hasDistributedReport = false,
        bool onTeamOrManager = true) =>
        EngagementJourneyResolver.Resolve(a, exceptions ?? [], hasCompletedReport, hasDistributedReport, userId, can, onTeamOrManager);

    private static AuditException OpenException(Guid auditId, Guid owner) => AuditException.Raise(
        auditId, Guid.NewGuid(), Guid.NewGuid(), "Gap", ExceptionSeverity.Medium, "root", "fix",
        "controls", "process_gap", owner, Manager, Target, targetDateOverridden: false, null, isRecurrence: false, null, "{}", Now);

    [Fact]
    public void Draft_without_setup_asks_the_manager_to_set_up()
    {
        var journey = Resolve(NewAudit(), Grants(PermissionKeys.ManageAudit), Manager);
        Assert.Equal("setup", journey.Stage);
        Assert.Contains(journey.NextActions, x => x.Code == "setup_engagement" && x.Kind == "route");
    }

    [Fact]
    public void Draft_ready_asks_the_manager_to_plan()
    {
        var (a, _) = Ready();
        var journey = Resolve(a, Grants(PermissionKeys.ManageAudit), Manager);
        var action = Assert.Single(journey.NextActions);
        Assert.Equal("plan_audit", action.Code);
        Assert.Equal("transition", action.Kind);
        Assert.Equal("planned", action.TargetState);
    }

    [Fact]
    public void Planned_asks_the_manager_to_launch_fieldwork()
    {
        var (a, _) = Ready();
        a.Plan();
        var journey = Resolve(a, Grants(PermissionKeys.ManageAudit), Manager);
        Assert.Equal("planned", journey.Stage);
        Assert.Contains(journey.NextActions, x => x.Code == "launch_fieldwork" && x.TargetState == "in_progress");
    }

    [Fact]
    public void Fieldwork_offers_respond_to_the_assigned_auditor_only()
    {
        var (a, _) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);

        var forAuditor = Resolve(a, Grants(PermissionKeys.RespondItem, PermissionKeys.RaiseException), Auditor);
        Assert.Equal("fieldwork", forAuditor.Stage);
        Assert.Contains(forAuditor.NextActions, x => x.Code == "respond_items" && x.Route!.EndsWith("/execute"));

        // A user who is not on the team (or lacks access) gets no actions.
        var outsider = Resolve(a, Grants(PermissionKeys.RespondItem), Guid.NewGuid(), onTeamOrManager: false);
        Assert.Empty(outsider.NextActions);
    }

    [Fact]
    public void A_finalised_fail_without_an_exception_offers_raise()
    {
        var (a, itemId) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);
        a.RecordResponse(itemId, ResponseVerdict.Fail, "no control", isDraft: false, Auditor, requireCommentOnPass: false, Now);

        var journey = Resolve(a, Grants(PermissionKeys.RaiseException), Auditor);
        Assert.Contains(journey.NextActions, x => x.Code == "raise_exception");
    }

    [Fact]
    public void All_responded_auto_reviews_and_lets_the_manager_complete_the_review()
    {
        var (a, itemId) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);
        // Responding the last item auto-transitions the aggregate to Under review.
        a.RecordResponse(itemId, ResponseVerdict.Pass, null, isDraft: false, Auditor, requireCommentOnPass: false, Now);

        // The lead first reviews the working paper; only then can the review be completed.
        var unreviewed = Resolve(a, Grants(PermissionKeys.ManageAudit), Manager);
        Assert.Equal("review", unreviewed.Stage);
        Assert.Contains(unreviewed.NextActions, x => x.Code == "sign_off_working_papers");
        Assert.DoesNotContain(unreviewed.NextActions, x => x.Code == "complete_review");

        a.SignOffItem(itemId, Manager, Now);
        var journey = Resolve(a, Grants(PermissionKeys.ManageAudit), Manager);
        Assert.DoesNotContain(journey.NextActions, x => x.Code == "sign_off_working_papers");
        Assert.Contains(journey.NextActions, x => x.Code == "complete_review" && x.TargetState == "completed");
    }

    [Fact]
    public void A_review_note_asks_the_team_to_answer_and_the_reviewer_to_clear()
    {
        var (a, itemId) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);
        a.RecordResponse(itemId, ResponseVerdict.Pass, null, isDraft: false, Auditor, requireCommentOnPass: false, Now);
        var note = a.RaiseReviewNote(itemId, "Reference the sample.", Manager, Now);

        Assert.Contains(Resolve(a, Grants(), Auditor).NextActions, x => x.Code == "answer_review_notes");
        Assert.DoesNotContain(Resolve(a, Grants(), Auditee).NextActions, x => x.Code == "answer_review_notes");

        a.AnswerReviewNote(note.Id, "Sample referenced in WP 3.", Auditor, Now);
        var forLead = Resolve(a, Grants(PermissionKeys.ManageAudit), Manager);
        Assert.Contains(forLead.NextActions, x => x.Code == "clear_review_notes");
        Assert.DoesNotContain(forLead.NextActions, x => x.Code == "complete_review");
    }

    [Fact]
    public void Completed_without_a_report_asks_to_generate_then_closes_once_distributed()
    {
        var (a, itemId) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);
        a.RecordResponse(itemId, ResponseVerdict.Pass, null, isDraft: false, Auditor, requireCommentOnPass: false, Now);
        a.SignOffItem(itemId, Manager, Now);
        a.Complete(Target); // already Under review via the auto-transition above

        var reporting = Resolve(a, Grants(PermissionKeys.GenerateReport), Manager);
        Assert.Equal("reporting", reporting.Stage);
        Assert.Contains(reporting.NextActions, x => x.Code == "generate_report");

        var closed = Resolve(a, Grants(PermissionKeys.GenerateReport, PermissionKeys.DistributeReport), Manager,
            hasCompletedReport: true, hasDistributedReport: true);
        Assert.Equal("closed", closed.Stage);
        Assert.Empty(closed.NextActions);
    }

    [Fact]
    public void Open_exception_asks_its_owner_to_submit_an_action_plan()
    {
        var (a, itemId) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);
        // The single Fail response auto-transitions the aggregate to Under review.
        a.RecordResponse(itemId, ResponseVerdict.Fail, "no control", isDraft: false, Auditor, requireCommentOnPass: false, Now);

        var ex = OpenException(a.Id, Auditee);
        var journey = Resolve(a, Grants(PermissionKeys.SubmitMap), Auditee, exceptions: [ex]);
        Assert.Equal("remediation", journey.Stage);
        Assert.Contains(journey.NextActions, x => x.Code == "submit_map" && x.Route == $"/exceptions/{ex.Id}");
    }

    [Fact]
    public void Cancelled_engagement_has_no_actions()
    {
        var a = NewAudit();
        a.Cancel("Engagement scope withdrawn by the audit committee.", Now);
        var journey = Resolve(a, Grants(PermissionKeys.ManageAudit), Manager);
        Assert.Equal("cancelled", journey.Stage);
        Assert.Empty(journey.NextActions);
    }

    /* ---- Engagement records ---- */

    private static EngagementRecordFacts Records(
        bool requireIndependence = true, EngagementLetterStatus? letter = EngagementLetterStatus.Acknowledged, bool kickoff = true,
        bool exit = false, bool draftReport = false, bool managesAudit = true)
        => new(requireIndependence, letter, kickoff, exit, draftReport, managesAudit);

    private static EngagementJourneyDto ResolveWith(Audit a, Func<string, bool> can, Guid userId, EngagementRecordFacts records, bool hasCompletedReport = false)
        => EngagementJourneyResolver.Resolve(a, [], hasCompletedReport, false, userId, can, onTeamOrManager: true, records);

    [Fact]
    public void Fieldwork_is_offered_only_once_the_whole_team_has_declared_independence()
    {
        var (a, _) = Ready();
        a.Plan();
        var manage = Grants(PermissionKeys.ManageAudit, PermissionKeys.ViewAudit);

        var waiting = ResolveWith(a, manage, Manager, Records());
        Assert.DoesNotContain(waiting.NextActions, x => x.Code == "launch_fieldwork");
        Assert.Contains(waiting.NextActions, x => x.Code == "declare_independence");
        Assert.Contains(ResolveWith(a, Grants(PermissionKeys.ViewAudit), Auditor, Records()).NextActions, x => x.Code == "declare_independence");

        a.DeclareIndependence(Manager, hasConflict: false, null, Now);
        a.DeclareIndependence(Auditor, hasConflict: false, null, Now);
        var ready = ResolveWith(a, manage, Manager, Records());
        Assert.Contains(ready.NextActions, x => x.Code == "launch_fieldwork");
        Assert.DoesNotContain(ready.NextActions, x => x.Code == "declare_independence");

        // Where the institution does not require declarations, fieldwork is offered straight away.
        var (b, _) = Ready();
        b.Plan();
        Assert.Contains(ResolveWith(b, manage, Manager, Records(requireIndependence: false)).NextActions, x => x.Code == "launch_fieldwork");
    }

    [Fact]
    public void A_disclosed_conflict_asks_a_manager_other_than_the_declarant_to_decide()
    {
        var (a, _) = Ready();
        a.Plan();
        a.DeclareIndependence(Auditor, hasConflict: true, "Worked in the branch last year.", Now);

        Assert.Contains(ResolveWith(a, Grants(PermissionKeys.ManageAudit), Manager, Records()).NextActions, x => x.Code == "decide_independence_conflicts");
        Assert.DoesNotContain(ResolveWith(a, Grants(PermissionKeys.ManageAudit), Auditor, Records()).NextActions, x => x.Code == "decide_independence_conflicts");
        Assert.DoesNotContain(ResolveWith(a, Grants(PermissionKeys.ViewAudit), Manager, Records(managesAudit: false)).NextActions, x => x.Code == "decide_independence_conflicts");
    }

    [Fact]
    public void The_lead_issues_the_letter_and_the_auditee_acknowledges_it()
    {
        var (a, _) = Ready();
        var lead = ResolveWith(a, Grants(PermissionKeys.ManageAudit), Manager, Records(letter: null));
        Assert.Contains(lead.NextActions, x => x.Code == "issue_engagement_letter");
        Assert.Contains(ResolveWith(a, Grants(PermissionKeys.ManageAudit), Manager, Records(letter: EngagementLetterStatus.Draft)).NextActions, x => x.Code == "issue_engagement_letter");
        Assert.DoesNotContain(ResolveWith(a, Grants(PermissionKeys.ManageAudit), Manager, Records(letter: EngagementLetterStatus.Issued)).NextActions, x => x.Code == "issue_engagement_letter");

        var auditee = ResolveWith(a, Grants(PermissionKeys.ViewAudit, PermissionKeys.ManageAudit), Auditee, Records(letter: EngagementLetterStatus.Issued));
        Assert.Contains(auditee.NextActions, x => x.Code == "acknowledge_engagement_letter");
        // The auditee never drives the audit side, whatever they hold elsewhere.
        Assert.DoesNotContain(auditee.NextActions, x => x.Code == "issue_engagement_letter");
        Assert.DoesNotContain(ResolveWith(a, Grants(PermissionKeys.ViewAudit), Auditee, Records()).NextActions, x => x.Code == "acknowledge_engagement_letter");
    }

    [Fact]
    public void The_team_is_asked_for_the_kickoff_minutes_in_fieldwork_and_the_exit_meeting_once_a_draft_exists()
    {
        var (a, item) = Ready();
        a.Plan();
        a.Start(requireIndependence: false);
        var manage = Grants(PermissionKeys.ManageAudit);
        Assert.Contains(ResolveWith(a, manage, Manager, Records(requireIndependence: false, kickoff: false)).NextActions, x => x.Code == "record_kickoff_minutes");
        Assert.DoesNotContain(ResolveWith(a, manage, Manager, Records(requireIndependence: false)).NextActions, x => x.Code == "record_kickoff_minutes");

        a.RecordResponse(item, ResponseVerdict.Pass, null, isDraft: false, Auditor, requireCommentOnPass: false, Now);
        Assert.Equal(AuditStatus.UnderReview, a.Status);
        Assert.DoesNotContain(ResolveWith(a, manage, Manager, Records(requireIndependence: false)).NextActions, x => x.Code == "record_exit_meeting");
        Assert.Contains(ResolveWith(a, manage, Manager, Records(requireIndependence: false, draftReport: true)).NextActions, x => x.Code == "record_exit_meeting");
        Assert.DoesNotContain(ResolveWith(a, manage, Manager, Records(requireIndependence: false, draftReport: true, exit: true)).NextActions, x => x.Code == "record_exit_meeting");
    }
}
