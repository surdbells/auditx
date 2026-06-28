using AuditX.Application.Abstractions.Ac;

namespace AuditX.Infrastructure.Ac;

/// <summary>
/// The format-dispatching <see cref="IAcPackRenderer"/> registered in DI (M13). Fans a render request out to the
/// concrete renderer that supports the requested format (HTML always; DOCX when OpenXml is wired). Mirrors the M8
/// <c>CompositeReportRenderer</c>.
/// </summary>
public sealed class CompositeAcPackRenderer : IAcPackRenderer
{
    private readonly IReadOnlyList<IAcPackRenderer> _renderers;

    public CompositeAcPackRenderer(IEnumerable<IAcPackRenderer> renderers)
    {
        _renderers = renderers.Where(r => r is not CompositeAcPackRenderer).ToArray();
        SupportedFormats = _renderers.SelectMany(r => r.SupportedFormats).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<string> SupportedFormats { get; }

    public bool CanRender(string format) => _renderers.Any(r => r.CanRender(format));

    public AcRenderedArtefact Render(string format, AcPackRenderContext context)
    {
        var renderer = _renderers.FirstOrDefault(r => r.CanRender(format))
            ?? throw new NotSupportedException($"No AC pack renderer is registered for format '{format}'.");
        return renderer.Render(format, context);
    }
}
