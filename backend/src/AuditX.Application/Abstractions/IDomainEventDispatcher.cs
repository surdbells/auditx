using AuditX.Domain.Common;

namespace AuditX.Application.Abstractions;

/// <summary>
/// Dispatches domain events raised by aggregates after their owning transaction commits. Subscribers
/// (notifications, analytics) react asynchronously; no module calls another module directly.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default);
}
