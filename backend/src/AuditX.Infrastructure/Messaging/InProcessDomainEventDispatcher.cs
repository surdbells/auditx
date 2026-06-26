using AuditX.Application.Abstractions;
using AuditX.Domain.Common;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Messaging;

/// <summary>
/// In-process domain-event dispatcher. It is the integration point for cross-cutting subscribers
/// (notifications in M10, analytics in M9); for the M1 slice there are no subscribers yet, so it
/// records the events for observability. A message-bus implementation can replace this without
/// changing callers.
/// </summary>
public sealed class InProcessDomainEventDispatcher(ILogger<InProcessDomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            logger.LogDebug("Domain event raised: {EventType} at {OccurredAt:o}", domainEvent.GetType().Name, domainEvent.OccurredAtUtc);
        }

        return Task.CompletedTask;
    }
}
