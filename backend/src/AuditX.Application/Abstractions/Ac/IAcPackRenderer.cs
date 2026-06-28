using AuditX.Application.Ac.Generation;

namespace AuditX.Application.Abstractions.Ac;

/// <summary>The full context an AC-pack renderer needs: the assembled composition + the CIA supplementary narrative.</summary>
public sealed record AcPackRenderContext(AcPackComposition Composition, string? CiaSupplementaryText);

/// <summary>A produced AC-pack artefact: its bytes, MIME type, suggested filename, format and the artefact's own SHA-256.</summary>
public sealed record AcRenderedArtefact(byte[] Content, string ContentType, string Filename, string Format, string Sha256Hash);

/// <summary>
/// Produces an AC-pack artefact in a requested format (M13). Mirrors the M8 <c>IReportRenderer</c>: the canonical,
/// hashed, browser-printable HTML (always) and DOCX via DocumentFormat.OpenXml (when requested). PDF is deferred.
/// The infrastructure facade dispatches by format and lists the formats it can render.
/// </summary>
public interface IAcPackRenderer
{
    /// <summary>The set of formats this renderer can produce (e.g. <c>["html","docx"]</c>).</summary>
    IReadOnlyCollection<string> SupportedFormats { get; }

    /// <summary>True if this renderer can produce <paramref name="format"/>.</summary>
    bool CanRender(string format);

    AcRenderedArtefact Render(string format, AcPackRenderContext context);
}

/// <summary>
/// Enqueues an AC pack's generation to run asynchronously off the request thread (M13). The implementation is a
/// fire-and-forget Hangfire job (the pack id IS the job handle). Abstracted so the command handler stays free of
/// the background-job framework.
/// </summary>
public interface IAcPackGenerationQueue
{
    void Enqueue(Guid acPackId);
}
