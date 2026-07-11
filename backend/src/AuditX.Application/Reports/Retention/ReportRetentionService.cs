using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Reports.Retention;

/// <summary>
/// Expires reports whose retention period has passed: their artefacts are no longer served, but the immutable
/// record + canonical hash are preserved (non-destructive). Run hourly by the ReportRetentionJob; idempotent —
/// only still-Completed reports past their retention date are affected, and a re-run finds none.
/// </summary>
public sealed class ReportRetentionService(
    IReportRepository reports, IClock clock, IAuditRecorder audit, IUnitOfWork unitOfWork, ILogger<ReportRetentionService> logger)
{
    /// <summary>Expires every completed report past its retention date; returns the number expired.</summary>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await reports.ListExpirableAsync(clock.UtcNow, cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var report in due)
        {
            report.Expire();
            audit.RecordAs(ActorType.System, "retention", actorUserId: null,
                AuditEventTypes.ReportExpired, AuditTargetTypes.Report, report.Id,
                after: new { report.VersionNumber, kind = report.Kind.ToString(), retentionUntil = report.RetentionUntil });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Expired {Count} report(s) past their retention date.", due.Count);
        return due.Count;
    }
}
