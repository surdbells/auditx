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
