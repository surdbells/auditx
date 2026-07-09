using AuditX.Application.Reports.Generation;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Reports;

/// <summary>
/// The full context a renderer needs to produce one artefact (M8). Discriminated by <see cref="Kind"/>: an
/// engagement report carries a <see cref="Composition"/> (per-audit); a standalone report carries a
/// <see cref="Standalone"/> model (cross-audit analytics). Exactly one payload is non-null.
/// </summary>
public sealed record ReportRenderContext(
    ReportKind Kind,
    ReportComposition? Composition,
    StandaloneReportModel? Standalone,
    string TemplateDefinitionJson,
    int TemplateVersion)
{
    /// <summary>Context for a per-audit engagement report.</summary>
    public static ReportRenderContext ForEngagement(ReportComposition composition, string templateDefinitionJson, int templateVersion)
        => new(ReportKind.AuditEngagement, composition, null, templateDefinitionJson, templateVersion);

    /// <summary>Context for a standalone (cross-audit) report.</summary>
    public static ReportRenderContext ForStandalone(StandaloneReportModel model, string templateDefinitionJson, int templateVersion)
        => new(model.Kind, null, model, templateDefinitionJson, templateVersion);
}

/// <summary>A produced report artefact: its bytes, MIME type, suggested filename, format, and the artefact's own SHA-256.</summary>
public sealed record RenderedArtefact(byte[] Content, string ContentType, string Filename, string Format, string Sha256Hash);

/// <summary>
/// Produces a report artefact in a requested format (M8). The v1 implementations render the canonical, hashed,
/// browser-printable HTML (always) and DOCX via DocumentFormat.OpenXml (when requested). Native PDF is a later
/// swap behind this port (license/audit risk — same stance as M7's <c>IDossierGenerator</c>). The infrastructure
/// facade dispatches by format and lists the formats it can render.
/// </summary>
public interface IReportRenderer
{
    /// <summary>The set of formats this renderer can produce (e.g. <c>["html","docx"]</c>).</summary>
    IReadOnlyCollection<string> SupportedFormats { get; }

    /// <summary>True if this renderer can produce <paramref name="format"/>.</summary>
    bool CanRender(string format);

    RenderedArtefact Render(string format, ReportRenderContext context);
}
