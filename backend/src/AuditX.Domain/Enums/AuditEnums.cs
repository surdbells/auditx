namespace AuditX.Domain.Enums;

/// <summary>Lifecycle state of an audit (M4).</summary>
public enum AuditStatus
{
    Draft,
    Planned,
    InProgress,
    UnderReview,
    Completed,
    Cancelled,
}

/// <summary>An audit team member's role on the engagement.</summary>
public enum TeamRole
{
    Lead,
    Auditor,
    Reviewer,
    Auditee,
}

/// <summary>
/// A reviewer's note on the working papers: <see cref="Open"/> until the team answers it, <see cref="Answered"/> until
/// a reviewer accepts the answer (<see cref="Cleared"/>) or reopens it.
/// </summary>
public enum ReviewNoteStatus
{
    Open,
    Answered,
    Cleared,
}

/// <summary>Per-item execution state on an audit checklist (advanced by M5 responses).</summary>
public enum ChecklistItemState
{
    NotStarted,
    InProgress,
    Responded,
}
