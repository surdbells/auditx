namespace AuditX.Domain.Enums;

/// <summary>Severity of an audit exception/finding (M6). Drives the default remediation target date.</summary>
public enum ExceptionSeverity
{
    Low,
    Medium,
    High,
    Critical,
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
