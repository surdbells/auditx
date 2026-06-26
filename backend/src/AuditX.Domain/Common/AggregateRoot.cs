namespace AuditX.Domain.Common;

/// <summary>
/// Base type for aggregate roots — the consistency boundary for a cluster of entities. Aggregate
/// roots collect domain events while their invariants are mutated; the events are dispatched after
/// the owning database transaction commits.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
