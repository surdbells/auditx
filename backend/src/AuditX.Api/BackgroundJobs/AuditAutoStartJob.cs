using AuditX.Application.Audits.Commands;
using AuditX.Application.Common.Messaging;

namespace AuditX.Api.BackgroundJobs;

/// <summary>Hourly job that moves planned audits whose start date has arrived to In Progress (US-M4-011).</summary>
public sealed class AuditAutoStartJob(IDispatcher dispatcher, ILogger<AuditAutoStartJob> logger)
{
    public const string RecurringJobId = "audit-auto-start";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var started = await dispatcher.Send(new AutoStartAuditsCommand(), cancellationToken);
        if (started > 0)
        {
            logger.LogInformation("Auto-started {Count} planned audit(s).", started);
        }
    }
}
