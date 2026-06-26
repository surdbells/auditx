using AuditX.Domain.Common;

namespace AuditX.Domain.Universe.Events;

public abstract record UniverseEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EntityCreatedEvent(Guid EntityId, string Name, string EntityType) : UniverseEvent;

public sealed record EntityRiskScoredEvent(
    Guid EntityId,
    decimal? CompositeInherentBefore,
    decimal? CompositeResidualBefore,
    decimal? CompositeInherentAfter,
    decimal? CompositeResidualAfter) : UniverseEvent;
