using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits.Events;

public abstract record AuditEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AuditCreatedEvent(Guid AuditId, string Name, string AuditType, Guid? PlanItemId) : AuditEvent;

public sealed record AuditTransitionedEvent(Guid AuditId, AuditStatus FromStatus, AuditStatus ToStatus, string? Reason) : AuditEvent;

public sealed record AuditCompletedEvent(Guid AuditId, Guid? PlanItemId, DateOnly ActualEndDate) : AuditEvent;

public sealed record AuditCancelledEvent(Guid AuditId, string Reason) : AuditEvent;

public sealed record AuditTeamMemberAddedEvent(Guid AuditId, Guid UserId, TeamRole Role) : AuditEvent;

public sealed record AuditTeamMemberRemovedEvent(Guid AuditId, Guid UserId) : AuditEvent;

public sealed record AuditLeadTransferredEvent(Guid AuditId, Guid OutgoingLeadUserId, Guid IncomingLeadUserId) : AuditEvent;
