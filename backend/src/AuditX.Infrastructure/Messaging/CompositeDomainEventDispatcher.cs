using System.Text.Json;
using System.Text.Json.Serialization;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Notifications;
using AuditX.Domain.Common;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Messaging;

/// <summary>
/// Post-commit domain-event dispatcher (M10). Logs each event and enqueues a durable Hangfire job to run
/// the notification pipeline off the request thread, so the interceptor stays cheap and delivery survives
/// process restarts. A message-bus implementation can replace this behind <see cref="IDomainEventDispatcher"/>.
/// </summary>
public sealed class CompositeDomainEventDispatcher(
    ILogger<CompositeDomainEventDispatcher> logger,
    IBackgroundJobClient backgroundJobs,
    ICurrentUser currentUser)
    : IDomainEventDispatcher
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUser.UserId;
        foreach (var domainEvent in events)
        {
            var eventType = NotificationEvents.Derive(domainEvent.GetType().Name);
            logger.LogDebug("Domain event raised: {EventType} at {OccurredAt:o}", eventType, domainEvent.OccurredAtUtc);

            var envelope = new DomainEventEnvelope(
                eventType,
                Guid.CreateVersion7(),
                domainEvent.OccurredAtUtc,
                actorUserId,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), PayloadOptions));

            backgroundJobs.Enqueue<NotificationIngestJob>(job => job.RunAsync(envelope, CancellationToken.None));
        }

        return Task.CompletedTask;
    }
}
