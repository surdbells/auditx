using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Scheduling;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Recurring Hangfire job (D3-C) that runs due report schedules. Lists the schedules due now, then processes each in
/// its OWN DI scope via <see cref="ScheduledReportRunner"/> so one schedule's failure (or dirty unit of work) can
/// never poison another's. <c>[DisableConcurrentExecution]</c> singleton so a slow batch never overlaps the next
/// hourly trigger and double-fires a schedule. Registered scoped in Program.cs and scheduled hourly.
/// </summary>
public sealed class ReportScheduleRunnerJob(
    IServiceScopeFactory scopeFactory, ILogger<ReportScheduleRunnerJob> logger)
{
    public const string RecurringJobId = "report-schedule-runner";

    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> dueIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var nowUtc = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
            var repository = scope.ServiceProvider.GetRequiredService<IReportScheduleRepository>();
            var due = await repository.ListDueAsync(nowUtc, cancellationToken);
            dueIds = due.Select(s => s.Id).ToList();
        }

        if (dueIds.Count == 0)
        {
            return;
        }

        logger.LogInformation("Report scheduler: {Count} schedule(s) due.", dueIds.Count);

        var produced = 0;
        foreach (var id in dueIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ScheduledReportRunner>();
                if (await runner.ExecuteAsync(id, cancellationToken))
                {
                    produced++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report scheduler: schedule {ScheduleId} failed; continuing with the rest.", id);
            }
        }

        logger.LogInformation("Report scheduler: produced {Produced} report(s) from {Due} due schedule(s).", produced, dueIds.Count);
    }
}
