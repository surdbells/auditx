using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions.Events;

namespace AuditX.Domain.Sanctions;

/// <summary>
/// A sanctions / disciplinary case (M7). Aggregate root over its case-team collection. Runs a lifecycle that is
/// DECOUPLED from the originating M6 <c>AuditException</c> (FR-M7-011): it never reads or writes exception state,
/// and the FK to the exception is <c>OnDelete(Restrict)</c>. The category/severity/recurrence keys used for grid
/// consultation are COPIED from the exception at <see cref="Trigger"/> so the case never re-loads it.
/// </summary>
public sealed class SanctionsCase : AggregateRoot
{
    private readonly List<SanctionsCaseTeamMember> _teamMembers = [];

    private SanctionsCase()
    {
    }

    public Guid ExceptionId { get; private set; }

    public Guid? SubjectUserId { get; private set; }

    public SanctionsCaseStatus Status { get; private set; }

    // Denormalised-from-exception lookup keys (set at trigger), so grid consult never re-loads the exception.
    public string? Category { get; private set; }

    public ExceptionSeverity Severity { get; private set; }

    public bool IsRecurrence { get; private set; }

    // Recommendation + grid pinning.
    public string? Recommendation { get; private set; }

    public int? GridConsultedVersion { get; private set; }

    public string? GridRecommendedRange { get; private set; }

    public bool WithinGridRange { get; private set; }

    public string? DeviationReason { get; private set; }

    // Authoritative decision detail (JSON); lifecycle timestamps promoted to columns for M9 metrics.
    public string? HrOutcomeJson { get; private set; }

    public string? DcDecisionJson { get; private set; }

    public Guid TriggeredBy { get; private set; }

    public DateTimeOffset TriggeredAt { get; private set; }

    public Guid? RecommendedBy { get; private set; }

    public DateTimeOffset? RecommendedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? HrOutcomeAt { get; private set; }

    public DateTimeOffset? DcDecisionAt { get; private set; }

    public Guid? ClosedBy { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<SanctionsCaseTeamMember> TeamMembers => _teamMembers.AsReadOnly();

    /// <summary>The sole construction path (FR-M7-003): a manual trigger off an exception. No event subscriber creates a case.</summary>
    public static SanctionsCase Trigger(
        Guid exceptionId, Guid? subjectUserId, string? category, ExceptionSeverity severity, bool isRecurrence,
        Guid triggeredBy, DateTimeOffset nowUtc)
    {
        var sanctionsCase = new SanctionsCase
        {
            ExceptionId = exceptionId,
            SubjectUserId = subjectUserId,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            Severity = severity,
            IsRecurrence = isRecurrence,
            Status = SanctionsCaseStatus.RecommendationDrafted,
            TriggeredBy = triggeredBy,
            TriggeredAt = nowUtc,
        };
        sanctionsCase._teamMembers.Add(new SanctionsCaseTeamMember(sanctionsCase.Id, triggeredBy, "investigator"));
        sanctionsCase.RaiseDomainEvent(new SanctionsTriggeredEvent(sanctionsCase.Id, exceptionId, subjectUserId, triggeredBy));
        return sanctionsCase;
    }

    /// <summary>
    /// Record (or revise) the recommendation while drafting. Load-bearing invariant (FR-M7-004): an out-of-grid-range
    /// recommendation requires a deviation reason (<c>sanctions.deviation_reason_required</c> → 422). Within range
    /// clears any prior reason. Pins the consulted grid version + range. Only valid in <c>recommendation_drafted</c>.
    /// </summary>
    public void RecordRecommendation(
        string recommendation, int? gridVersion, string? gridRange, bool withinRange, string? deviationReason,
        Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureStatus("sanctions.recommendation_locked", SanctionsCaseStatus.RecommendationDrafted);
        Guard.NotNullOrWhiteSpace(recommendation, "sanctions.recommendation_required", "A recommendation is required.");

        if (!withinRange && string.IsNullOrWhiteSpace(deviationReason))
        {
            throw new DomainException("sanctions.deviation_reason_required", "A deviation reason is required when the recommendation falls outside the grid range.");
        }

        Recommendation = recommendation.Trim();
        GridConsultedVersion = gridVersion;
        GridRecommendedRange = string.IsNullOrWhiteSpace(gridRange) ? null : gridRange.Trim();
        WithinGridRange = withinRange;
        DeviationReason = withinRange ? null : deviationReason!.Trim();
        RecommendedBy = actorId;
        RecommendedAt = nowUtc;
        RaiseDomainEvent(new SanctionsRecommendedEvent(Id, gridVersion, withinRange, actorId));
    }

    /// <summary>Submit the drafted recommendation for HR routing. Requires a recorded recommendation.</summary>
    public void SubmitRecommendation(Guid actorId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(Recommendation))
        {
            throw new DomainException("sanctions.recommendation_required", "A recommendation must be recorded before submission.");
        }

        Transition(SanctionsCaseStatus.RecommendationSubmitted, SanctionsCaseStatus.RecommendationDrafted);
        SubmittedAt = nowUtc;
        RaiseDomainEvent(new SanctionsRecommendationSubmittedEvent(Id, actorId));
    }

    /// <summary>
    /// Record the HR authority's outcome. From <c>recommendation_submitted</c>: a <c>dc_referral</c> outcome moves to
    /// <c>dc_referral</c>; any other outcome moves to <c>hr_outcome_recorded</c>.
    /// </summary>
    public void RecordHrOutcome(HrOutcomeType outcomeType, string detailJson, Guid actorId, DateTimeOffset nowUtc)
    {
        Guard.NotNullOrWhiteSpace(detailJson, "sanctions.hr_detail_required", "HR outcome detail is required.");
        var target = outcomeType == HrOutcomeType.DcReferral ? SanctionsCaseStatus.DcReferral : SanctionsCaseStatus.HrOutcomeRecorded;
        Transition(target, SanctionsCaseStatus.RecommendationSubmitted);
        HrOutcomeJson = detailJson;
        HrOutcomeAt = nowUtc;
        if (outcomeType == HrOutcomeType.DcReferral)
        {
            RaiseDomainEvent(new DcReferralEvent(Id, actorId));
        }
        else
        {
            RaiseDomainEvent(new HrOutcomeRecordedEvent(Id, outcomeType, actorId));
        }
    }

    /// <summary>Refer the case to the disciplinary committee from <c>recommendation_submitted</c>. Reason ≥ 20 chars.</summary>
    public void ReferToDc(string reason, Guid actorId, DateTimeOffset nowUtc)
    {
        Guard.MinLength(reason, 20, "sanctions.referral_reason_required", "A referral reason of at least 20 characters is required.");
        Transition(SanctionsCaseStatus.DcReferral, SanctionsCaseStatus.RecommendationSubmitted);
        // A direct referral is NOT an HR outcome — don't stamp HrOutcomeAt (it would pollute the
        // recommendation→HR-outcome turnaround metric). The referral time is captured by the dc_referral trail entry.
        RaiseDomainEvent(new DcReferralEvent(Id, actorId));
    }

    /// <summary>Record the DC's decision from <c>dc_referral</c>.</summary>
    public void RecordDcDecision(DcDecisionType decision, string detailJson, Guid actorId, DateTimeOffset nowUtc)
    {
        Guard.NotNullOrWhiteSpace(detailJson, "sanctions.dc_detail_required", "DC decision detail is required.");
        Transition(SanctionsCaseStatus.DcDecisionRecorded, SanctionsCaseStatus.DcReferral);
        DcDecisionJson = detailJson;
        DcDecisionAt = nowUtc;
        RaiseDomainEvent(new DcDecisionRecordedEvent(Id, decision, actorId));
    }

    /// <summary>Mark the case as appealed (driven by the appeal aggregate's handler) from a decision state.</summary>
    public void MarkAppealed()
        => Transition(SanctionsCaseStatus.Appealed, SanctionsCaseStatus.HrOutcomeRecorded, SanctionsCaseStatus.DcDecisionRecorded);

    /// <summary>Record the appeal outcome on the case trail (FR-M7-009 propagation) from <c>appealed</c>.</summary>
    public void RecordAppealOutcome(AppealOutcome outcome, Guid appealId, Guid decidedBy)
    {
        Transition(SanctionsCaseStatus.AppealDecisionRecorded, SanctionsCaseStatus.Appealed);
        RaiseDomainEvent(new AppealOutcomeRecordedEvent(appealId, Id, outcome, decidedBy));
    }

    /// <summary>Explicit closure (B5/C1) from any decision state. Terminal.</summary>
    public void Close(Guid actorId, DateTimeOffset nowUtc)
    {
        Transition(
            SanctionsCaseStatus.Closed,
            SanctionsCaseStatus.HrOutcomeRecorded, SanctionsCaseStatus.DcDecisionRecorded, SanctionsCaseStatus.AppealDecisionRecorded);
        ClosedBy = actorId;
        ClosedAt = nowUtc;
        RaiseDomainEvent(new SanctionsCaseClosedEvent(Id, actorId));
    }

    /// <summary>Add an actor to the case team if not already present (idempotent), for the confidentiality predicate.</summary>
    public void EnsureTeamMember(Guid userId, string roleMarker)
    {
        if (_teamMembers.All(m => m.UserId != userId))
        {
            _teamMembers.Add(new SanctionsCaseTeamMember(Id, userId, roleMarker));
        }
    }

    private void Transition(SanctionsCaseStatus to, params SanctionsCaseStatus[] from)
    {
        if (from.Length > 0 && !from.Contains(Status))
        {
            throw new InvalidStateTransitionException("sanctions.invalid_transition", $"Cannot move to {to} from {Status}.");
        }

        Status = to;
    }

    private void EnsureStatus(string code, params SanctionsCaseStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStateTransitionException(code, $"Operation not permitted while the case is {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
