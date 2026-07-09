using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Enums;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// The canonical artefact filename stem for a report render (M8) — <c>audit-report-{auditId}-v{n}</c> for an
/// engagement report, <c>{kind-stem}-v{n}</c> for a standalone report. Renderers append the format extension.
/// </summary>
public static class ReportArtefactNaming
{
    public static string Stem(ReportRenderContext context)
        => context.Kind == ReportKind.AuditEngagement && context.Composition is { } c
            ? $"audit-report-{c.AuditId}-v{c.VersionNumber}"
            : context.Standalone is { } model
                ? $"{model.FileStem}-v{model.VersionNumber}"
                : "report";
}
