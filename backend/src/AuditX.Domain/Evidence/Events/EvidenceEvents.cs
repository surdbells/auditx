using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Evidence.Events;

public abstract record EvidenceEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EvidenceUploadedEvent(Guid EvidenceId, Guid AuditId, EvidenceContextType ContextType, Guid ContextId, Guid UploadedBy) : EvidenceEvent;

public sealed record EvidenceHashMismatchEvent(Guid EvidenceId, Guid AuditId, string ExpectedHash) : EvidenceEvent;

public sealed record EvidenceUnflaggedEvent(Guid EvidenceId, Guid AuditId, string Resolution) : EvidenceEvent;

/// <summary>
/// An auditor requested a document from an auditee (P2-D). Carries the auditee so the notification pipeline can
/// email them a link to upload it. <see cref="AuditName"/> and <see cref="DueDate"/> populate the message body.
/// </summary>
public sealed record EvidenceRequestedEvent(
    Guid EvidenceRequestId, Guid AuditId, string AuditName, Guid RequestedFromUserId, Guid RequestedByUserId, string Title, string? DueDate) : EvidenceEvent;
