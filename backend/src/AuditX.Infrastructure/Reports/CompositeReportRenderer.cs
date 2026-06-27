using AuditX.Application.Abstractions.Reports;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// The format-dispatching <see cref="IReportRenderer"/> registered in DI (M8). It fans a render request out to
/// the concrete renderer that supports the requested format (HTML always; DOCX when OpenXml is wired). If DOCX is
/// not registered (the documented HTML-only fallback), <see cref="CanRender"/> simply returns false for docx and
/// the generation service skips that format.
/// </summary>
public sealed class CompositeReportRenderer : IReportRenderer
{
    private readonly IReadOnlyList<IReportRenderer> _renderers;

    public CompositeReportRenderer(IEnumerable<IReportRenderer> renderers)
    {
        // Exclude self to avoid a recursive registration if this type is ever resolved into the set.
        _renderers = renderers.Where(r => r is not CompositeReportRenderer).ToArray();
        SupportedFormats = _renderers.SelectMany(r => r.SupportedFormats).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<string> SupportedFormats { get; }

    public bool CanRender(string format) => _renderers.Any(r => r.CanRender(format));

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        var renderer = _renderers.FirstOrDefault(r => r.CanRender(format))
            ?? throw new NotSupportedException($"No report renderer is registered for format '{format}'.");
        return renderer.Render(format, context);
    }
}
