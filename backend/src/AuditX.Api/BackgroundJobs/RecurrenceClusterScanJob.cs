using AuditX.Application.Abstractions;
using AuditX.Application.Analytics.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Daily Hangfire job (M9, G6) that detects exception recurrence. Runs <see cref="RecurrenceClusterService"/> to
/// upsert recurrence clusters, records the scan via <see cref="IAuditRecorder.RecordAs"/> (System actor), then
/// saves through the unit of work so the newly-detected clusters' <c>RecurrenceClusterDetectedEvent</c>s dispatch
/// post-commit (→ M10 notification rule → Audit Manager). Idempotent: re-runs only notify on a NEW threshold cross.
/// </summary>
public sealed class RecurrenceClusterScanJob(
    RecurrenceClusterService service, IAuditRecorder audit, IUnitOfWork unitOfWork, ILogger<RecurrenceClusterScanJob> logger)
{
    public const string RecurringJobId = "recurrence-cluster-scan";

    // Singleton: never let a slow scan overlap the next daily trigger (avoids duplicate cluster creation on the
    // same (entity, category) key before the unique index commits, and double-counted detections).
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var newlyDetected = await service.ScanAsync(cancellationToken);

        // Record the scan even when nothing new crossed the threshold (the trail proves the job ran). System actor.
        audit.RecordAs(ActorType.System, RecurringJobId, null,
            AuditEventTypes.RecurrenceClusterDetected, AuditTargetTypes.RecurrenceCluster, null,
            payload: new { newlyDetected });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (newlyDetected > 0)
        {
            logger.LogInformation("Recurrence scan detected {Count} new cluster(s).", newlyDetected);
        }
    }
}
