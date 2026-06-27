using AuditX.Application.Reports.Generation;

namespace AuditX.Application.Abstractions.Reports;

/// <summary>The full context a renderer needs to produce one artefact (assembled composition + the snapshotted template).</summary>
public sealed record ReportRenderContext(ReportComposition Composition, string TemplateDefinitionJson, int TemplateVersion);

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
