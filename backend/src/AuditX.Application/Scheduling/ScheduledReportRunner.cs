using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Scheduling;

/// <summary>
/// Executes a single due <see cref="Domain.Scheduling.ReportSchedule"/> (D3-C): generates a fresh standalone report
/// version and emails it to the schedule's recipients, as the System actor. Called once per due schedule (each in its
/// own DI scope) by the recurring runner job, so one schedule's failure never poisons another's unit of work.
/// Generation is SYNCHRONOUS here (not enqueued) so the completed artefact is in hand to distribute in the same run.
/// </summary>
public sealed class ScheduledReportRunner(
    IReportScheduleRepository schedules,
    IReportRepository reports,
    IReportTemplateRepository templates,
    IUserRepository users,
    ReportGenerationService generation,
    IEmailSender emailSender,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<ScheduledReportRunner> logger)
{
    /// <summary>The audit-trail actor label for schedule-driven activity.</summary>
    public const string Source = "report-schedule-runner";

    /// <summary>
    /// Generate + deliver the schedule identified by <paramref name="scheduleId"/> if it is still due. Returns true
    /// when a report row was produced (even a failed one — the run IS recorded and the schedule advanced).
    /// </summary>
    public async Task<bool> ExecuteAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var nowUtc = clock.UtcNow;
        var schedule = await schedules.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null || !schedule.IsDue(nowUtc))
        {
            // Deleted, deactivated or already advanced since the due-scan listed it — nothing to do.
            return false;
        }

        // 1) Create the standalone report version + advance the schedule in one commit, so even if generation or the
        //    process dies next, the run is durably recorded and this slot never re-fires on the following tick.
        var template = await templates.GetActiveAsync(cancellationToken);
        var templateId = template?.Id ?? Guid.Empty;
        var templateVersion = template?.VersionNumber ?? 0;
        var templateJson = template?.TemplateDefinitionJson
            ?? $"{{\"title\":\"{StandaloneReportModel.TitleFor(schedule.Kind)}\"}}";

        var nextVersion = await reports.GetNextVersionForKindAsync(schedule.Kind, cancellationToken) + 1;
        var report = Report.StartStandalone(
            schedule.Kind, nextVersion, templateId, templateVersion, templateJson, ["html"], actorId: Guid.Empty, nowUtc);
        reports.Add(report);
        audit.RecordAs(ActorType.System, Source, null,
            AuditEventTypes.ReportGenerationRequested, AuditTargetTypes.Report, report.Id,
            after: new { kind = schedule.Kind.ToString(), report.VersionNumber, status = ReportStatus.Pending.ToString() });

        schedule.RecordRun(report.Id, nowUtc);
        audit.RecordAs(ActorType.System, Source, null,
            AuditEventTypes.ReportScheduleRun, AuditTargetTypes.ReportSchedule, schedule.Id,
            after: new { reportId = report.Id, schedule.NextRunAt });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 2) Generate synchronously (idempotent on the report id). On failure the service marks the report failed.
        await generation.RunAsync(report.Id, cancellationToken);

        // 3) Distribute the completed report to the schedule's recipients.
        var completed = await reports.GetByIdAsync(report.Id, cancellationToken);
        if (completed is null || completed.Status != ReportStatus.Completed)
        {
            logger.LogWarning("Scheduled report {ReportId} (schedule {ScheduleId}) did not complete; skipping delivery.", report.Id, scheduleId);
            return true;
        }

        var recipients = ReportScheduleRecipients.Parse(schedule.RecipientsJson);
        await DistributeAsync(completed, recipients, nowUtc, cancellationToken);
        return true;
    }

    private async Task DistributeAsync(
        Report report, ReportScheduleRecipients recipients, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // Resolve and dedupe recipients: directory users (by id → email) + ad-hoc external addresses.
        var resolvedUsers = await users.GetByIdsAsync(recipients.UserIds.Distinct().ToArray(), cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<(Guid? UserId, string? AdHocEmail, string Email)>();

        foreach (var user in resolvedUsers)
        {
            var email = user.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(email) && seen.Add(email))
            {
                targets.Add((user.Id, null, email));
            }
        }

        foreach (var raw in recipients.Emails)
        {
            var email = raw?.Trim();
            if (!string.IsNullOrWhiteSpace(email) && seen.Add(email))
            {
                targets.Add((null, email, email));
            }
        }

        if (targets.Count == 0)
        {
            logger.LogWarning("Scheduled report {ReportId} has no resolvable recipients; skipping delivery.", report.Id);
            return;
        }

        var reportName = StandaloneReportModel.TitleFor(report.Kind);
        var subject = $"Scheduled report: {reportName}";
        var body = $"The {reportName} report (version {report.VersionNumber}) was generated automatically by a report schedule.";

        var recorded = 0;
        var failed = 0;
        foreach (var (userId, adHocEmail, email) in targets)
        {
            // Send FIRST; only record a distribution row for a delivery that actually left the building, so the log
            // never overstates delivery (mirrors DistributeReportCommand).
            ChannelSendResult result;
            try
            {
                result = await emailSender.SendAsync(email, subject, body, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Scheduled report {ReportId} delivery to a recipient threw.", report.Id);
                failed++;
                continue;
            }

            if (!result.Success)
            {
                logger.LogWarning("Scheduled report {ReportId} delivery to a recipient failed: {Error}", report.Id, result.Error);
                failed++;
                continue;
            }

            report.RecordDistribution(userId, adHocEmail, email, dispatchedBy: Guid.Empty, nowUtc, reportName, 0, 0, "no exceptions");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            recorded++;
        }

        if (recorded > 0)
        {
            audit.RecordAs(ActorType.System, Source, null,
                AuditEventTypes.ReportDistributed, AuditTargetTypes.Report, report.Id,
                after: new { report.VersionNumber, recipientCount = recorded, failedCount = failed });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
