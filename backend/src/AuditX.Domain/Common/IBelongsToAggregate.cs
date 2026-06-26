namespace AuditX.Domain.Common;

/// <summary>
/// Marks a child entity that lives inside an aggregate boundary. The persistence layer uses this to
/// advance the owning aggregate root's concurrency token (rowversion) whenever a child is added,
/// modified or removed — so collection-only mutations still participate in optimistic concurrency
/// even though they never touch a scalar on the root row.
/// </summary>
public interface IBelongsToAggregate
{
    Guid AggregateRootId { get; }
}
