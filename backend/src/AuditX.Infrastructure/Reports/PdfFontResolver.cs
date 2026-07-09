using PdfSharp.Fonts;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// PDFsharp font resolver for the cross-platform (GDI-free) build (M8). The slim aspnet Linux image ships no fonts,
/// so a regular + bold TrueType face is read directly from disk: Liberation Sans (Arial-metric-compatible, installed
/// via the Dockerfile) or DejaVu on Linux, or Arial on a Windows dev/test host. One family (regular + bold) is enough
/// for the report layout; italics fall back to the upright face.
/// </summary>
public sealed class PdfFontResolver : IFontResolver
{
    /// <summary>The single family name the PDF renderer sets on its styles.</summary>
    public const string FamilyName = "AuditX Sans";

    private const string RegularFace = "auditx-regular";
    private const string BoldFace = "auditx-bold";

    private static readonly string[] RegularCandidates =
    [
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        @"C:\Windows\Fonts\arial.ttf",
    ];

    private static readonly string[] BoldCandidates =
    [
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
    ];

    private readonly byte[] _regular;
    private readonly byte[] _bold;

    public PdfFontResolver()
    {
        _regular = Load(RegularCandidates)
            ?? throw new InvalidOperationException(
                "No TrueType font was found for PDF rendering. Install a font package (e.g. fonts-liberation) on the host.");
        _bold = Load(BoldCandidates) ?? _regular;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        => new FontResolverInfo(bold ? BoldFace : RegularFace);

    public byte[]? GetFont(string faceName)
        => faceName == BoldFace ? _bold : _regular;

    private static byte[]? Load(IEnumerable<string> candidates)
    {
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        return null;
    }
}
