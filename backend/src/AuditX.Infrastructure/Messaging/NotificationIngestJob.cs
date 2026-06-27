using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Notifications.Services;

namespace AuditX.Infrastructure.Messaging;

/// <summary>Hangfire job that runs the M10 notification pipeline for one raised domain event.</summary>
public sealed class NotificationIngestJob(NotificationIngestService ingest)
{
    public Task RunAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken)
        => ingest.ProcessAsync(envelope, cancellationToken);
}
