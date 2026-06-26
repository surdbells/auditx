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

/// <summary>Per-item execution state on an audit checklist (advanced by M5 responses).</summary>
public enum ChecklistItemState
{
    NotStarted,
    InProgress,
    Responded,
}
