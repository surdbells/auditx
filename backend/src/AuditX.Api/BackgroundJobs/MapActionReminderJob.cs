using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Daily Hangfire job: reminders through each remediation action's life. For every open action of an agreed plan it
/// sends, once each, a "due soon" reminder to the action's owner <see cref="DueSoonDays"/> days ahead, an "overdue"
/// notice the day after the due date, and an escalation to the owner's line manager once it is
/// <see cref="EscalateAfterDays"/> days overdue. The domain records the stage reached on the action, so a re-run sends
/// nothing twice; an approved extension starts the sequence again. Notifications go through the M10 pipeline
/// (<c>map_action_due_soon</c>, <c>map_action_overdue</c>, <c>map_action_escalated</c> rules).
/// </summary>
public sealed class MapActionReminderJob(
    IExceptionRepository exceptions,
    IClock clock,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork,
    ILogger<MapActionReminderJob> logger)
{
    public const string RecurringJobId = "map-action-reminders";

    /// <summary>How many days before an action is due its owner is reminded.</summary>
    public const int DueSoonDays = 7;

    /// <summary>How many days overdue an action must be before it goes to the owner's line manager.</summary>
    public const int EscalateAfterDays = 14;

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var findings = await exceptions.ListForActionRemindersAsync(today.AddDays(DueSoonDays), cancellationToken);
        var sent = 0;
        foreach (var finding in findings)
        {
            sent += finding.SendActionReminders(today, now, DueSoonDays, EscalateAfterDays);
        }

        // Record the run even when nothing was sent (the trail proves the job ran). System actor.
        audit.RecordAs(ActorType.System, RecurringJobId, null,
            AuditEventTypes.MapActionRemindersSent, AuditTargetTypes.Exception, null,
            payload: new { sent, findings = findings.Count });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (sent > 0)
        {
            logger.LogInformation("Sent {Count} remediation-action reminder(s) across {Findings} finding(s).", sent, findings.Count);
        }
    }
}
