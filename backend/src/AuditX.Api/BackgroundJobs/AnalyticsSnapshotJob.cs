using AuditX.Application.Analytics.Services;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Daily Hangfire job that captures the current KPI projections into the append-only analytics_snapshots fact
/// table (via <see cref="AnalyticsSnapshotCaptureService"/>), giving trend/time-series reporting a backing that
/// the otherwise point-in-time analytics queries lack. Idempotent per day (the store replaces the day's rows).
/// </summary>
public sealed class AnalyticsSnapshotJob(
    AnalyticsSnapshotCaptureService capture, ILogger<AnalyticsSnapshotJob> logger)
{
    public const string RecurringJobId = "analytics-snapshot-capture";

    // Singleton: a slow capture must never overlap the next trigger and double-write the same day.
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var rows = await capture.CaptureAsync(cancellationToken);
        logger.LogInformation("Captured {Count} analytics snapshot row(s).", rows);
    }
}
