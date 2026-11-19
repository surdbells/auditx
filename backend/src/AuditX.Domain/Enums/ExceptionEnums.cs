namespace AuditX.Domain.Enums;

/// <summary>Severity of an audit exception/finding (M6). Drives the default remediation target date.</summary>
public enum ExceptionSeverity
{
    Low,
    Medium,
    High,
    Critical,
}

/// <summary>Lifecycle of a systemic root-cause gap tracked across findings.</summary>
public enum RootCauseGapStatus
{
    Open,
    Closed,
}

/// <summary>Status of a single remediation action within a root-cause gap's remediation plan.</summary>
public enum RootCauseGapRemediationStatus
{
    Open,
    Completed,
}

/// <summary>
/// The exception lifecycle (M6). Exactly seven persisted states (PRD/BRD authoritative). The Critical-closure
/// CIA hold is modelled as the <c>CiaPending</c> flag within <see cref="PendingClosure"/>, not an eighth status.
/// </summary>
public enum ExceptionStatus
{
    Open,
    MapSubmitted,
    MapApproved,
    MapRejected,
    PendingClosure,
    Closed,
    Cancelled,
}

/// <summary>State of a single remediation action within a Management Action Plan.</summary>
public enum MapActionStatus
{
    Pending,
    Complete,

    /// <summary>
    /// The action belonged to a plan that a reopen replaced (for example after a failed follow-up verification). It is
    /// kept as history but no longer counts towards the current plan, its completion or its evidence.
    /// </summary>
    Superseded,
}

/// <summary>
/// How far the daily reminder job has gone for one remediation action. Each stage is sent once; an approved
/// due-date extension starts the sequence again.
/// </summary>
public enum MapActionReminderStage
{
    None,
    DueSoon,
    Overdue,
    Escalated,
}

/// <summary>State of a request to move a finding's or an action's due date.</summary>
public enum ExtensionRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Withdrawn,
}

/// <summary>Management's formal position on a finding (P2-B), distinct from the remediation plan.</summary>
public enum ManagementResponseDecision
{
    Accepted,
    PartiallyAccepted,
    Disputed,
}

/// <summary>Outcome of a post-closure follow-up verification that remediation is effective (P2-B).</summary>
public enum VerificationResult
{
    Passed,
    PartiallyPassed,
    Failed,
}
