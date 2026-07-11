using AuditX.Application.Reports.Retention;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Daily Hangfire job that expires reports past their configured retention period (their artefacts stop being served;
/// the immutable record + canonical hash are preserved — non-destructive). A no-op when no retention policy is set or
/// nothing is due. Singleton so a slow run never overlaps the next trigger.
/// </summary>
public sealed class ReportRetentionJob(ReportRetentionService retention, ILogger<ReportRetentionJob> logger)
{
    public const string RecurringJobId = "report-retention-expiry";

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var expired = await retention.ExpireDueAsync(cancellationToken);
        if (expired > 0)
        {
            logger.LogInformation("Report retention: expired {Count} report(s) past their retention date.", expired);
        }
    }
}
