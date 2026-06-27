namespace AuditX.Domain.Enums;

/// <summary>
/// The sanctions-case lifecycle (M7). Persisted snake_case (via <c>ToSnake</c>). The state machine is the
/// SCOPED slice: <c>recommendation_drafted → recommendation_submitted → {hr_outcome_recorded | dc_referral}</c>,
/// each decision state branching to <c>closed</c> or <c>appealed → appeal_decision_recorded → closed</c>.
/// (The PRD's <c>hr_routing</c> and <c>returned_to_audit</c> states are deferred — see the M7 blueprint.)
/// </summary>
public enum SanctionsCaseStatus
{
    RecommendationDrafted,
    RecommendationSubmitted,
    HrOutcomeRecorded,
    DcReferral,
    DcDecisionRecorded,
    Appealed,
    AppealDecisionRecorded,
    Closed,
}

/// <summary>The HR authority's outcome on a recommended sanction (M7). <c>DcReferral</c> escalates to the committee.</summary>
public enum HrOutcomeType
{
    Imposed,
    Declined,
    Modified,
    DcReferral,
}

/// <summary>The disciplinary committee's decision on a referred case (M7).</summary>
public enum DcDecisionType
{
    Uphold,
    Modify,
    Dismiss,
}

/// <summary>Lifecycle of an appeal against a sanction (M7). <c>InReview</c> is reserved/unused in v1 (file → decided directly).</summary>
public enum AppealStatus
{
    Filed,
    InReview,
    Decided,
}

/// <summary>The appeals authority's outcome on a filed appeal (M7).</summary>
public enum AppealOutcome
{
    Confirm,
    Modify,
    Overturn,
}
