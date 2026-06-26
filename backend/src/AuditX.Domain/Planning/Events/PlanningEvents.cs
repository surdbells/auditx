using AuditX.Domain.Common;

namespace AuditX.Domain.Planning.Events;

public abstract record PlanningEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PlanSubmittedEvent(Guid PlanId, string PeriodLabel) : PlanningEvent;

public sealed record PlanDecidedEvent(Guid PlanId, string Decision, Guid DecidedBy) : PlanningEvent;

public sealed record PlanClosedEvent(Guid PlanId) : PlanningEvent;

public sealed record PlanItemLinkedToAuditEvent(Guid PlanItemId, Guid AuditId) : PlanningEvent;
