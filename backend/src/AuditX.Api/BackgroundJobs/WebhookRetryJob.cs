using AuditX.Application.Integrations.Webhooks;

namespace AuditX.Api.BackgroundJobs;

/// <summary>Sweeps failed webhook deliveries whose backoff has elapsed and re-attempts them (US-M14-010).</summary>
public sealed class WebhookRetryJob(WebhookDispatchService dispatcher)
{
    public const string RecurringJobId = "webhook-retry";

    public Task RunAsync(CancellationToken cancellationToken) => dispatcher.ProcessDueRetriesAsync(cancellationToken);
}
