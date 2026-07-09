using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Reports.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Reports.Generation;

/// <summary>
/// Runs a report's generation off the request thread (M8). Invoked by the Hangfire <c>ReportGenerationJob</c>.
/// IDEMPOTENT on the report id (D2/BR-CF-021): a re-run (double-submit, Hangfire requeue) is a no-op unless the
/// report is still <c>pending</c>, so generation never produces a duplicate version. On success it assembles the
/// composition, renders each requested format, stores the artefacts via <see cref="IFileStorage"/>, hashes the
/// canonical HTML, completes the report (post-commit → <c>ReportGenerated</c> → M10 + trail). Any failure marks
/// the report <c>failed</c> with the reason.
/// </summary>
public sealed class ReportGenerationService(
    IReportRepository reports,
    IAuditRepository audits,
    ReportContentAssembler assembler,
    StandaloneReportAssembler standaloneAssembler,
    IReportRenderer renderer,
    IFileStorage storage,
    IClock clock,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    ILogger<ReportGenerationService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(reportId, cancellationToken);
        if (report is null)
        {
            logger.LogWarning("Report {ReportId} not found; skipping generation.", reportId);
            return;
        }

        // Idempotency: only a still-pending report is processed. A requeue/double-run sees running/completed/failed
        // and exits, so no duplicate version is ever produced (BR-CF-021).
        if (report.Status != ReportStatus.Pending)
        {
            logger.LogInformation("Report {ReportId} is {Status}; generation already handled.", reportId, report.Status);
            return;
        }

        try
        {
            report.MarkRunning();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            ReportRenderContext context;
            if (report.Kind == ReportKind.AuditEngagement)
            {
                var auditId = report.AuditId
                    ?? throw new InvalidOperationException($"Engagement report {reportId} has no audit.");
                var audit = await audits.GetByIdAsync(auditId, cancellationToken)
                    ?? throw new InvalidOperationException($"Audit {auditId} for report {reportId} no longer exists.");
                var composition = await assembler.AssembleAsync(audit, report.VersionNumber, cancellationToken);
                context = ReportRenderContext.ForEngagement(composition, report.TemplateDefinitionSnapshotJson, report.TemplateVersionSnapshot);
            }
            else
            {
                var model = await standaloneAssembler.AssembleAsync(report.Kind, report.VersionNumber, cancellationToken);
                context = ReportRenderContext.ForStandalone(model, report.TemplateDefinitionSnapshotJson, report.TemplateVersionSnapshot);
            }

            var requestedFormats = ReportMappings.ParseFormats(report.RequestedFormatsJson);
            var produced = new List<ProducedArtefact>();
            string? canonicalHash = null;

            foreach (var format in requestedFormats)
            {
                if (!renderer.CanRender(format))
                {
                    logger.LogWarning("Report {ReportId}: format '{Format}' is not supported by the active renderer; skipping.", reportId, format);
                    continue;
                }

                var artefact = renderer.Render(format, context);
                var key = $"reports/{report.Id}/v{report.VersionNumber}/{format}/{Guid.NewGuid():N}.{format}";
                await storage.SaveAsync(key, artefact.Content, cancellationToken);
                produced.Add(new ProducedArtefact(format, key, artefact.ContentType, artefact.Content.LongLength, artefact.Sha256Hash));

                if (format == "html")
                {
                    canonicalHash = artefact.Sha256Hash;
                }
            }

            // The canonical, hashed artefact is always the HTML one (it is always produced).
            canonicalHash ??= produced.FirstOrDefault()?.Sha256
                ?? throw new InvalidOperationException($"Report {reportId} produced no artefacts.");

            var producedJson = JsonSerializer.Serialize(produced, JsonOptions);
            report.Complete(canonicalHash, produced, producedJson, clock.UtcNow);
            auditRecorder.RecordAs(Domain.Enums.ActorType.System, "reports", report.GeneratedBy,
                AuditEventTypes.ReportGenerated, AuditTargetTypes.Report, report.Id,
                after: new { report.AuditId, report.VersionNumber, sha256 = canonicalHash, formats = produced.Count });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Report {ReportId} (audit {AuditId} v{Version}) generated with {Count} artefact(s).",
                reportId, report.AuditId, report.VersionNumber, produced.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Report {ReportId} generation failed.", reportId);
            await FailReportAsync(reportId, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task FailReportAsync(Guid reportId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            // Reload on a clean state so the failure mark commits even if the tracked graph is dirty.
            var report = await reports.GetByIdAsync(reportId, cancellationToken);
            if (report is null || report.Status is ReportStatus.Completed or ReportStatus.Failed)
            {
                return;
            }

            report.Fail(Truncate(reason, 1000));
            auditRecorder.RecordAs(Domain.Enums.ActorType.System, "reports", report.GeneratedBy,
                AuditEventTypes.ReportGenerationFailed, AuditTargetTypes.Report, report.Id,
                after: new { report.AuditId, report.VersionNumber, reason = Truncate(reason, 1000) });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark report {ReportId} as failed.", reportId);
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? "Generation failed." : value.Length <= max ? value : value[..max];
}
