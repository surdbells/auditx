using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Fire-and-forget Hangfire job that runs a report's generation off the request thread (M8). Enqueued from the
/// generate handler via <see cref="IReportGenerationQueue"/>; resolves the application <see cref="ReportGenerationService"/>.
/// The service is idempotent on the report id, so a Hangfire requeue never produces a duplicate version.
/// </summary>
public sealed class ReportGenerationJob(ReportGenerationService service)
{
    public Task RunAsync(Guid reportId, CancellationToken cancellationToken)
        => service.RunAsync(reportId, cancellationToken);
}

/// <summary>Enqueues <see cref="ReportGenerationJob"/> as a fire-and-forget Hangfire job (the report id IS the handle).</summary>
public sealed class HangfireReportGenerationQueue(IBackgroundJobClient backgroundJobs) : IReportGenerationQueue
{
    public void Enqueue(Guid reportId)
        => backgroundJobs.Enqueue<ReportGenerationJob>(job => job.RunAsync(reportId, CancellationToken.None));
}
