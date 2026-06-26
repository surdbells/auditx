namespace AuditX.Domain.Common;

/// <summary>
/// Marker for an immutable fact that has happened within the domain. Domain events are
/// raised by aggregate roots and dispatched after the owning transaction commits so that
/// cross-cutting subscribers (audit trail, notifications, analytics) can react.
/// </summary>
public interface IDomainEvent
{
    /// <summary>UTC instant at which the event was raised.</summary>
    DateTimeOffset OccurredAtUtc { get; }
}
