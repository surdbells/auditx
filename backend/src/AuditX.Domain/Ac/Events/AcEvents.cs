using AuditX.Domain.Common;

namespace AuditX.Domain.Ac.Events;

public abstract record AcEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when AC-pack generation is requested (status → generated). Drives the trail; M10 routes off Generated.</summary>
public sealed record AcPackGenerationRequestedEvent(Guid AcPackId, int VersionNumber, Guid RequestedBy) : AcEvent;

/// <summary>
/// Raised when an AC pack finishes rendering (status → pending_review). M10 routes <c>ac_pack_generated</c> to the
/// CIA role so the review/approve gate is actioned.
/// </summary>
public sealed record AcPackGeneratedEvent(Guid AcPackId, int VersionNumber, Guid GeneratedBy) : AcEvent;

/// <summary>Raised when AC-pack generation fails (status → failed).</summary>
public sealed record AcPackGenerationFailedEvent(Guid AcPackId, int VersionNumber, string FailureReason) : AcEvent;

/// <summary>Raised when the CIA approves an AC pack (status → approved).</summary>
public sealed record AcPackApprovedEvent(Guid AcPackId, int VersionNumber, Guid ApprovedBy) : AcEvent;

/// <summary>
/// Raised per recipient when an approved AC pack is distributed (M13). <see cref="RecipientUserId"/> is a directory
/// user id (the AC cohort is resolved internally); M10 routes <c>ac_pack_distributed</c> via
/// <c>payload_derived: RecipientUserId</c> so each committee member is notified individually.
/// </summary>
public sealed record AcPackDistributedEvent(
    Guid AcPackId,
    int VersionNumber,
    Guid RecipientUserId,
    string? AcMeetingLabel) : AcEvent;

/// <summary>Raised when an AC action item is created (status → open). M10 routes <c>ac_action_item_created</c> to the CIA role.</summary>
public sealed record AcActionItemCreatedEvent(Guid AcActionItemId, string Title, Guid CreatedBy) : AcEvent;

/// <summary>Raised when the AC chair acknowledges a closed action item (status → acknowledged_closed; terminal).</summary>
public sealed record AcActionItemClosureAcknowledgedEvent(Guid AcActionItemId, Guid AcknowledgedBy) : AcEvent;

/// <summary>Raised when an AC comment is added on a plan/pack/finding. M10 routes <c>ac_comment_added</c> to the CIA role.</summary>
public sealed record AcCommentAddedEvent(Guid AcCommentId, string TargetType, Guid TargetId, Guid AuthorUserId) : AcEvent;

/// <summary>Raised when the CIA sets/updates a restricted-visibility allow-list on a finding (M13, FR-M13-009).</summary>
public sealed record FindingVisibilityRestrictedEvent(string FindingType, Guid FindingId, Guid RestrictedBy) : AcEvent;

/// <summary>
/// Raised when verify-on-read detects an AC-pack artefact SHA-256 mismatch (mirrors the M8 report alert). Payload
/// carries <c>Severity="Critical"</c> so the M10 suppression-override + SMS escalation fires for security recipients.
/// </summary>
public sealed record AcPackHashMismatchEvent(
    Guid AcPackId,
    string ExpectedHash,
    string RecomputedHash,
    Guid DetectedBy,
    string Severity = "Critical") : AcEvent;
