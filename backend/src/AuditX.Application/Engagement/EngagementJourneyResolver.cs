using AuditX.Application.Common.Concurrency;
using AuditX.Application.Common.Enums;
using AuditX.Application.Engagement.Dtos;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Engagement;

/// <summary>
/// The "next best action" engine. Given an engagement's state and the signed-in user's role/permissions,
/// it derives the lifecycle stage and the concrete next step(s) that user can take. Pure and deterministic
/// so it is trivially unit-testable; the query handlers supply the loaded aggregate + a permission predicate.
/// Every action is emitted only when the user both holds the required permission AND is on the audit team
/// (or manages it) — the same authorization the target screens enforce.
/// </summary>
/// <summary>
/// What the journey needs to know about an audit's engagement records beyond the aggregate: whether fieldwork waits
/// for independence declarations, the engagement letter's state (null when none is written), whether the kickoff and
/// exit minutes are recorded, whether a completed draft report exists, and whether the caller manages the audit
/// (may decide on a disclosed conflict).
/// </summary>
public sealed record EngagementRecordFacts(
    bool RequireIndependence,
    EngagementLetterStatus? LetterStatus,
    bool KickoffRecorded,
    bool ExitRecorded,
    bool HasDraftReport,
    bool ManagesAudit,
    bool Loaded = true)
{
    /// <summary>The records were not loaded (or there are none to keep: a self-assessment): no record-based actions.</summary>
    public static readonly EngagementRecordFacts None = new(false, null, false, false, false, false, Loaded: false);
}

public static class EngagementJourneyResolver
{
    /// <summary>The canonical ordered lifecycle stages, with English labels (the UI may localise by code).</summary>
    private static readonly (string Code, string Label)[] StageOrder =
    [
        ("setup", "Setup"),
        ("planned", "Planned"),
        ("fieldwork", "Fieldwork"),
        ("review", "Under review"),
        ("remediation", "Remediation"),
        ("reporting", "Reporting"),
        ("closed", "Closed"),
    ];

    public static EngagementJourneyDto Resolve(
        Audit audit,
        IReadOnlyList<AuditException> exceptions,
        bool hasCompletedReport,
        bool hasDistributedReport,
        Guid userId,
        Func<string, bool> can,
        bool onTeamOrManager,
        EngagementRecordFacts? records = null)
    {
        records ??= EngagementRecordFacts.None;
        var totalItems = audit.ChecklistItems.Count;
        var respondedItems = audit.ChecklistItems.Count(i => i.ItemState == ChecklistItemState.Responded);
        var progress = totalItems == 0 ? 0 : (int)Math.Round(100.0 * respondedItems / totalItems);

        var openExceptions = exceptions
            .Where(e => e.Status is ExceptionStatus.Open or ExceptionStatus.MapSubmitted
                or ExceptionStatus.MapRejected or ExceptionStatus.PendingClosure)
            .ToArray();

        var stageCode = CurrentStage(audit.Status, openExceptions.Length > 0, hasCompletedReport, hasDistributedReport);
        var stages = BuildTimeline(stageCode, audit.Status == AuditStatus.Cancelled);
        var actions = onTeamOrManager
            ? NextActions(audit, openExceptions, hasCompletedReport, hasDistributedReport, userId, can, records)
            : [];

        return new EngagementJourneyDto(
            audit.Id, audit.Name, audit.AuditType, audit.Status.ToSnake(),
            stageCode, progress, openExceptions.Length, RowVersionToken.Encode(audit.Version), stages, actions, CreatedAt: audit.CreatedAt);
    }

    private static string CurrentStage(AuditStatus status, bool anyOpenException, bool hasCompletedReport, bool hasDistributedReport)
        => status switch
        {
            AuditStatus.Cancelled => "cancelled",
            AuditStatus.Draft => "setup",
            AuditStatus.Planned => "planned",
            AuditStatus.InProgress => "fieldwork",
            AuditStatus.UnderReview => anyOpenException ? "remediation" : "review",
            AuditStatus.Completed => anyOpenException ? "remediation"
                : (hasCompletedReport && hasDistributedReport) ? "closed" : "reporting",
            _ => "setup",
        };

    private static IReadOnlyList<LifecycleStageDto> BuildTimeline(string currentCode, bool cancelled)
    {
        // Cancelled engagements show every stage as inactive with a terminal 'cancelled' marker.
        if (cancelled)
        {
            return [.. StageOrder.Select(s => new LifecycleStageDto(s.Code, s.Label, "pending"))];
        }

        var currentIndex = Array.FindIndex(StageOrder, s => s.Code == currentCode);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        return [.. StageOrder.Select((s, i) => new LifecycleStageDto(
            s.Code, s.Label,
            i < currentIndex ? "done" : i == currentIndex ? "current" : "pending"))];
    }

    private static IReadOnlyList<NextActionDto> NextActions(
        Audit audit,
        IReadOnlyList<AuditException> openExceptions,
        bool hasCompletedReport,
        bool hasDistributedReport,
        Guid userId,
        Func<string, bool> can,
        EngagementRecordFacts records)
    {
        var actions = new List<NextActionDto>();
        var auditRoute = $"/audits/{audit.Id}";
        var executeRoute = $"/audits/{audit.Id}/execute";

        var hasChecklist = audit.ChecklistItems.Count > 0;
        // The working team only: an audit can have several auditees and none of them is its auditor.
        var hasAuditor = audit.TeamMembers.Any(m => m.IsActive && !audit.IsAuditee(m.UserId));
        var allResponded = audit.ChecklistItems.Count > 0
            && audit.ChecklistItems.All(i => i.ItemState == ChecklistItemState.Responded);
        var myPending = audit.ChecklistItems.Count(i =>
            i.AssignedUserId == userId && i.ItemState != ChecklistItemState.Responded);
        var failsWithoutException = audit.ChecklistItems.Count(i => !i.HasException
            && audit.Responses.Any(r => r.ChecklistItemId == i.Id && !r.IsDraft && r.Verdict == ResponseVerdict.Fail));

        // Working-paper review: answered items still to sign off (not the reviewer's own work, no note outstanding on
        // them), answered notes waiting on a reviewer, and open notes waiting on the team.
        var isReviewer = audit.IsReviewer(userId);
        var toSignOff = audit.ChecklistItems.Count(i => i.ItemState == ChecklistItemState.Responded && i.ReviewedBy is null
            && audit.Responses.Any(r => r.ChecklistItemId == i.Id && !r.IsDraft && r.ResponderUserId != userId)
            && !audit.ReviewNotes.Any(n => n.ChecklistItemId == i.Id && n.IsOutstanding));
        var notesToClear = audit.ReviewNotes.Count(n => n.Status == ReviewNoteStatus.Answered);
        var notesToAnswer = audit.ReviewNotes.Count(n => n.Status == ReviewNoteStatus.Open);
        var reviewComplete = audit.IsSelfAssessment
            || (audit.ChecklistItems.All(i => i.ReviewedBy is not null) && !audit.ReviewNotes.Any(n => n.IsOutstanding));

        // Engagement records. The team (with ManageAudit) writes the letter and records the meetings; each member
        // declares their own independence; the audit's managers decide on disclosed conflicts; the auditee acknowledges.
        var independent = !audit.IsSelfAssessment && records.Loaded;
        var teamManages = independent && audit.IsOnAuditTeam(userId) && can(PermissionKeys.ManageAudit);
        var independenceOutstanding = independent && records.RequireIndependence ? audit.IndependenceOutstanding().Count : 0;
        var mine = audit.DeclarationOf(userId);
        var conflictsToDecide = audit.IndependenceDeclarations.Count(d => d.Status == IndependenceStatus.ConflictDisclosed && d.UserId != userId);
        var independenceOpen = audit.Status is AuditStatus.Draft or AuditStatus.Planned or AuditStatus.InProgress or AuditStatus.UnderReview;

        if (independent && records.RequireIndependence && independenceOpen && audit.IsOnAuditTeam(userId) && mine is null)
        {
            actions.Add(Route("declare_independence", "Declare your independence", auditRoute, PermissionKeys.ViewAudit));
        }

        if (independent && independenceOpen && conflictsToDecide > 0 && records.ManagesAudit && !audit.IsAuditee(userId))
        {
            actions.Add(Route("decide_independence_conflicts", $"Decide on {conflictsToDecide} disclosed conflict(s) of interest", auditRoute, PermissionKeys.ManageAudit));
        }

        if (teamManages && audit.Status is AuditStatus.Draft or AuditStatus.Planned or AuditStatus.InProgress
            && records.LetterStatus is null or EngagementLetterStatus.Draft)
        {
            actions.Add(Route("issue_engagement_letter", "Issue the engagement letter", auditRoute, PermissionKeys.ManageAudit));
        }

        if (independent && audit.IsAuditee(userId) && records.LetterStatus == EngagementLetterStatus.Issued && audit.Status != AuditStatus.Cancelled)
        {
            actions.Add(Route("acknowledge_engagement_letter", "Acknowledge the engagement letter", auditRoute, PermissionKeys.ViewAudit));
        }

        if (teamManages && audit.Status is AuditStatus.InProgress or AuditStatus.UnderReview && !records.KickoffRecorded)
        {
            actions.Add(Route("record_kickoff_minutes", "Record the kickoff meeting minutes", auditRoute, PermissionKeys.ManageAudit));
        }

        if (teamManages && audit.Status is AuditStatus.UnderReview or AuditStatus.Completed && records.HasDraftReport && !records.ExitRecorded)
        {
            actions.Add(Route("record_exit_meeting", "Record the exit meeting on the draft report", auditRoute, PermissionKeys.ManageAudit));
        }

        switch (audit.Status)
        {
            case AuditStatus.Draft:
                if (can(PermissionKeys.ManageAudit))
                {
                    actions.Add(hasChecklist && hasAuditor
                        ? Transition("plan_audit", "Plan audit", "planned")
                        : Route("setup_engagement", "Set up team & checklist", auditRoute, PermissionKeys.ManageAudit));
                }
                break;

            case AuditStatus.Planned:
                if (can(PermissionKeys.ManageAudit))
                {
                    // Fieldwork waits until the whole team has declared independence (where the institution requires it).
                    if (independenceOutstanding == 0)
                    {
                        actions.Add(Transition("launch_fieldwork", "Launch fieldwork", "in_progress"));
                    }
                }
                break;

            case AuditStatus.InProgress:
                if (myPending > 0 && can(PermissionKeys.RespondItem))
                {
                    actions.Add(Route("respond_items", $"Respond to {myPending} assigned item(s)", executeRoute, PermissionKeys.RespondItem));
                }
                if (failsWithoutException > 0 && can(PermissionKeys.RaiseException))
                {
                    actions.Add(Route("raise_exception", $"Raise exception on {failsWithoutException} failed item(s)", executeRoute, PermissionKeys.RaiseException));
                }
                AddReviewActions(actions, executeRoute, isReviewer, audit.IsOnAuditTeam(userId), toSignOff, notesToClear, notesToAnswer);
                // Note: the aggregate auto-transitions to Under review once every item is responded, so
                // there is no manual "submit for review" step to surface here.
                break;

            case AuditStatus.UnderReview:
                if (failsWithoutException > 0 && can(PermissionKeys.RaiseException))
                {
                    actions.Add(Route("raise_exception", $"Raise exception on {failsWithoutException} failed item(s)", executeRoute, PermissionKeys.RaiseException));
                }
                AddRemediationActions(actions, openExceptions, userId, can);
                AddReviewActions(actions, executeRoute, isReviewer, audit.IsOnAuditTeam(userId), toSignOff, notesToClear, notesToAnswer);
                // The audit completes only once its working papers are fully reviewed.
                if (openExceptions.Count == 0 && reviewComplete && can(PermissionKeys.ManageAudit))
                {
                    actions.Add(Transition("complete_review", "Complete review", "completed"));
                }
                break;

            case AuditStatus.Completed:
                AddRemediationActions(actions, openExceptions, userId, can);
                if (openExceptions.Count == 0)
                {
                    if (!hasCompletedReport && can(PermissionKeys.GenerateReport))
                    {
                        actions.Add(Route("generate_report", "Generate report", auditRoute, PermissionKeys.GenerateReport));
                    }
                    else if (hasCompletedReport && !hasDistributedReport && can(PermissionKeys.DistributeReport))
                    {
                        actions.Add(Route("distribute_report", "Distribute report", auditRoute, PermissionKeys.DistributeReport));
                    }
                }
                break;
        }

        return actions;
    }

    private static void AddReviewActions(
        List<NextActionDto> actions, string executeRoute, bool isReviewer, bool onTeam, int toSignOff, int notesToClear, int notesToAnswer)
    {
        if (isReviewer && toSignOff > 0)
        {
            actions.Add(Route("sign_off_working_papers", $"Review and sign off {toSignOff} item(s)", executeRoute, PermissionKeys.ViewAudit));
        }

        if (isReviewer && notesToClear > 0)
        {
            actions.Add(Route("clear_review_notes", $"Clear or reopen {notesToClear} answered review note(s)", executeRoute, PermissionKeys.ViewAudit));
        }

        if (onTeam && notesToAnswer > 0)
        {
            actions.Add(Route("answer_review_notes", $"Answer {notesToAnswer} review note(s)", executeRoute, PermissionKeys.ViewAudit));
        }
    }

    private static void AddRemediationActions(
        List<NextActionDto> actions, IReadOnlyList<AuditException> openExceptions, Guid userId, Func<string, bool> can)
    {
        // Owner's action plan is due (or was returned) on an exception they own.
        var mine = openExceptions.FirstOrDefault(e => e.OwnerUserId == userId
            && e.Status is ExceptionStatus.Open or ExceptionStatus.MapRejected);
        if (mine is not null && can(PermissionKeys.SubmitMap))
        {
            actions.Add(Route("submit_map", "Submit action plan", $"/exceptions/{mine.Id}", PermissionKeys.SubmitMap));
        }

        var toApprove = openExceptions.FirstOrDefault(e => e.Status == ExceptionStatus.MapSubmitted);
        if (toApprove is not null && can(PermissionKeys.ApproveMap))
        {
            actions.Add(Route("approve_map", "Review submitted action plan", $"/exceptions/{toApprove.Id}", PermissionKeys.ApproveMap));
        }

        var toClose = openExceptions.FirstOrDefault(e => e.Status == ExceptionStatus.PendingClosure);
        if (toClose is not null && can(PermissionKeys.CloseException))
        {
            actions.Add(Route("close_exception", "Verify & close exception", $"/exceptions/{toClose.Id}", PermissionKeys.CloseException));
        }
    }

    private static NextActionDto Route(string code, string label, string route, string permissionKey)
        => new(code, label, "route", route, null, permissionKey);

    private static NextActionDto Transition(string code, string label, string targetState)
        => new(code, label, "transition", null, targetState, PermissionKeys.ManageAudit);
}
