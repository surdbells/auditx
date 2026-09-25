using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Daily Hangfire job (P2): escalate overdue management action plans up the reporting line. Finds open findings whose
/// MAP response is past its due/target date and not yet escalated, calls <c>EscalateMapOverdue</c> on each (raising a
/// <c>MapOverdueEscalatedEvent</c>), records a System-actor audit entry, and saves — so the M10 pipeline notifies each
/// owner's line manager via the seeded <c>map_overdue_escalated → line_manager(OwnerUserId)</c> rule. Idempotent: the
/// domain sets <c>MapOverdueEscalatedAt</c>, so each finding escalates exactly once.
/// </summary>
public sealed class MapOverdueEscalationJob(
    IExceptionRepository exceptions,
    IClock clock,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork,
    ILogger<MapOverdueEscalationJob> logger)
{
    public const string RecurringJobId = "map-overdue-escalation";

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var overdue = await exceptions.ListOverdueForEscalationAsync(today, cancellationToken);
        var escalated = 0;
        foreach (var ex in overdue)
        {
            var due = ex.ManagementResponseDueDate ?? ex.TargetDate;
            var daysOverdue = Math.Max(0, today.DayNumber - due.DayNumber);
            if (ex.EscalateMapOverdue(now, daysOverdue))
            {
                escalated++;
            }
        }

        // Record the run even when nothing escalated (the trail proves the job ran). System actor.
        audit.RecordAs(ActorType.System, RecurringJobId, null,
            AuditEventTypes.MapOverdueEscalated, AuditTargetTypes.Exception, null,
            payload: new { escalated, scanned = overdue.Count });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (escalated > 0)
        {
            logger.LogInformation("Escalated {Count} overdue MAP(s) up the reporting line.", escalated);
        }
    }
}
