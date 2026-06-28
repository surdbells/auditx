namespace AuditX.Domain.Enums;

/// <summary>
/// The Audit-Committee pack generation lifecycle (M13). Persisted snake_case (via <c>SnakeCaseEnumConverter</c>).
/// Mirrors the M8 report status but adds the CIA review/approve/distribute gate:
/// <c>generated → pending_review → approved → distributed</c>, with <c>failed</c> as the terminal failure state.
/// A completed (PendingReview onward) pack's <see cref="AuditX.Domain.Ac.AcPack.ContentSnapshotJson"/> is immutable.
/// </summary>
public enum AcPackStatus
{
    /// <summary>The pack row exists; generation has been requested but not yet started.</summary>
    Generated,

    /// <summary>The generation worker is rendering the artefacts.</summary>
    Running,

    /// <summary>Generation finished; the pack is sealed and awaits CIA review/approval.</summary>
    PendingReview,

    /// <summary>The CIA has approved the pack for distribution to the audit committee.</summary>
    Approved,

    /// <summary>The pack has been distributed to at least one audit-committee recipient.</summary>
    Distributed,

    /// <summary>Generation failed. Terminal; regeneration is a new pack version.</summary>
    Failed,
}

/// <summary>
/// The lifecycle of an Audit-Committee action item (M13). Persisted snake_case. <c>open → in_progress → closed</c>
/// with a REQUIRED closure response, then the AC chair may move it to <c>acknowledged_closed</c> (terminal).
/// </summary>
public enum AcActionItemStatus
{
    Open,
    InProgress,
    Closed,
    AcknowledgedClosed,
}

/// <summary>
/// The delivery outcome of a single AC-pack distribution (M13). Defaults to <see cref="Pending"/>; the inbound
/// M14 relay callback that flips it to <see cref="Delivered"/>/<see cref="Bounced"/> is deferred.
/// </summary>
public enum AcDeliveryOutcome
{
    Pending,
    Delivered,
    Bounced,
}

/// <summary>The polymorphic target an AC comment can be attached to (M13, A3/BR-M13-010).</summary>
public enum AcCommentTargetType
{
    Plan,
    Pack,
    Finding,
}

/// <summary>
/// The kind of finding a restricted-visibility allow-list applies to (M13, FR-M13-009). Today only audit
/// exceptions are restrictable; the enum is the extension point for future finding sources.
/// </summary>
public enum FindingType
{
    Exception,
}
