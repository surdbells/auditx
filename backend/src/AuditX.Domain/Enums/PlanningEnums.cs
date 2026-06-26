namespace AuditX.Domain.Enums;

/// <summary>Lifecycle state of an annual audit plan (M3). Rejection is a decision outcome, not a status.</summary>
public enum PlanStatus
{
    Draft,
    Submitted,
    RevisionsRequested,
    Approved,
    RevisionSubmitted,
    Closed,
}

/// <summary>Execution status of a single plan item, driven by the linked audit's lifecycle (M4).</summary>
public enum PlanItemStatus
{
    Planned,
    InProgress,
    Completed,
    Deferred,
}

/// <summary>The decision an Audit Committee chair records on a submitted plan.</summary>
public enum AcDecisionOutcome
{
    Approved,
    RevisionsRequested,
    Rejected,
}
