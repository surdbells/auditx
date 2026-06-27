using AuditX.Application.Notifications.Services;

namespace AuditX.Api.BackgroundJobs;

/// <summary>Sweeps failed notification dispatches whose retry time has arrived and re-attempts them (US-M10-005).</summary>
public sealed class NotificationRetryJob(NotificationIngestService ingest, ILogger<NotificationRetryJob> logger)
{
    public const string RecurringJobId = "notification-retry";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var retried = await ingest.RetryDueAsync(cancellationToken);
        if (retried > 0)
        {
            logger.LogInformation("Retried {Count} notification dispatch(es).", retried);
        }
    }
}
