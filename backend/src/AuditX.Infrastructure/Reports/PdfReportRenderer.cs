using System.Security.Cryptography;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// Renders the report as a true, server-generated PDF via PDFsharp/MigraDoc (MIT, pure-managed cross-platform build —
/// no <c>libgdiplus</c>) (M8). Snapshot-faithful: it projects the SAME assembled composition / standalone model the
/// HTML renderer uses (via <see cref="ReportTabularProjection"/>), one heading + table per section, under the
/// institution's letterhead (logo, name, brand colours) with a generated-at line and page footer. Carries its own
/// SHA-256; the canonical/verifiable hash remains the HTML's.
/// </summary>
public sealed class PdfReportRenderer : IReportRenderer
{
    // A4 portrait, 2cm margins → ~17cm of usable content width for the tables.
    private const double UsableWidthCm = 17.0;

    // The logo is drawn at most this tall / wide, keeping its aspect ratio.
    private const double LogoMaxHeightCm = 1.4;
    private const double LogoMaxWidthCm = 5.0;

    static PdfReportRenderer()
    {
        // The GDI-free build has no built-in font source; register our disk-backed resolver once, process-wide.
        GlobalFontSettings.FontResolver ??= new PdfFontResolver();
    }

    public IReadOnlyCollection<string> SupportedFormats { get; } = ["pdf"];

    public bool CanRender(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        var (title, subtitle) = Header(context);
        var sheets = ReportTabularProjection.From(context);

        var document = BuildDocument(title, subtitle, sheets, context.Branding);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        var bytes = stream.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(bytes, "application/pdf", $"{ReportArtefactNaming.Stem(context)}.pdf", "pdf", sha256);
    }

    private static (string Title, string Subtitle) Header(ReportRenderContext context)
    {
        if (context.Kind == ReportKind.AuditEngagement && context.Composition is { } c)
        {
            return (c.AuditName, $"Audit report v{c.VersionNumber} — generated {c.GeneratedAtUtc:yyyy-MM-dd} (UTC)");
        }

        return context.Standalone is { } model ? (model.Title, model.Subtitle) : ("Report", string.Empty);
    }

    private static Document BuildDocument(string title, string subtitle, IReadOnlyList<ReportSheet> sheets, ReportBranding branding)
    {
        var palette = new BrandPalette(branding);
        var headingColor = ToColor(palette.HeadingText);

        var document = new Document();
        document.Info.Title = title;
        document.Info.Author = branding.InstitutionName;

        var normal = document.Styles["Normal"]!;
        normal.Font.Name = PdfFontResolver.FamilyName;
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        AppendLetterhead(section, branding, palette);

        var titleParagraph = section.AddParagraph(title);
        titleParagraph.Format.Font.Size = 16;
        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.Font.Color = headingColor;
        titleParagraph.Format.SpaceAfter = Unit.FromPoint(2);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleParagraph = section.AddParagraph(subtitle);
            subtitleParagraph.Format.Font.Size = 9;
            subtitleParagraph.Format.Font.Color = Colors.Gray;
            subtitleParagraph.Format.SpaceAfter = Unit.FromPoint(6);
        }

        foreach (var sheet in sheets)
        {
            var heading = section.AddParagraph(sheet.Name);
            heading.Format.Font.Size = 12;
            heading.Format.Font.Bold = true;
            heading.Format.Font.Color = headingColor;
            heading.Format.SpaceBefore = Unit.FromPoint(12);
            heading.Format.SpaceAfter = Unit.FromPoint(4);
            heading.Format.KeepWithNext = true;
            heading.Format.Borders.Bottom.Width = 0.75;
            heading.Format.Borders.Bottom.Color = ToColor(palette.Accent);
            heading.Format.Borders.DistanceFromBottom = Unit.FromPoint(2);
            AppendTable(section, sheet, palette);
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText($"{branding.InstitutionName} · Generated by AuditX · page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        return document;
    }

    /// <summary>
    /// The letterhead: the institution's logo (when it is a PNG or JPEG this engine can embed) beside its name, closed
    /// by a rule in the primary brand colour. A logo in another format (SVG, WebP, GIF) is simply omitted — the name
    /// and colours still brand the page.
    /// </summary>
    private static void AppendLetterhead(Section section, ReportBranding branding, BrandPalette palette)
    {
        var logo = EmbeddableLogo(branding);

        var table = section.AddTable();
        table.Borders.Visible = false;
        table.LeftPadding = 0;
        table.RightPadding = 0;

        var logoWidthCm = logo is { } l ? l.WidthCm + 0.4 : 0;
        if (logo is not null)
        {
            table.AddColumn(Unit.FromCentimeter(logoWidthCm));
        }

        table.AddColumn(Unit.FromCentimeter(UsableWidthCm - logoWidthCm));

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;
        var nameCell = row.Cells[logo is null ? 0 : 1];
        if (logo is { } embedded)
        {
            var image = row.Cells[0].AddImage("base64:" + Convert.ToBase64String(embedded.Bytes));
            image.LockAspectRatio = true;
            image.Height = Unit.FromCentimeter(embedded.HeightCm);
        }

        var name = nameCell.AddParagraph(branding.InstitutionName);
        name.Format.Font.Size = 13;
        name.Format.Font.Bold = true;
        name.Format.Font.Color = ToColor(palette.HeadingText);

        var rule = section.AddParagraph();
        rule.Format.Font.Size = 2;
        rule.Format.SpaceBefore = Unit.FromPoint(4);
        rule.Format.SpaceAfter = Unit.FromPoint(12);
        rule.Format.Borders.Bottom.Width = 2;
        rule.Format.Borders.Bottom.Color = ToColor(palette.Primary);
    }

    /// <summary>
    /// The logo's bytes and drawn size if this engine can embed it: a PNG or JPEG that PDFsharp actually loads
    /// (checked up front, so an exotic image can never fail the whole report at render time).
    /// </summary>
    private static (byte[] Bytes, double WidthCm, double HeightCm)? EmbeddableLogo(ReportBranding branding)
    {
        if (branding.DecodeLogo() is not { } logo
            || !ImageHeader.TryRead(logo.Bytes, out _, out var width, out var height))
        {
            return null;
        }

        try
        {
            using var probe = XImage.FromStream(new MemoryStream(logo.Bytes));
        }
        catch (Exception)
        {
            // Not an image this build can decode (e.g. an unusual PNG variant) — brand with name + colours only.
            return null;
        }

        var aspect = (double)width / height;
        var heightCm = LogoMaxHeightCm;
        var widthCm = heightCm * aspect;
        if (widthCm > LogoMaxWidthCm)
        {
            widthCm = LogoMaxWidthCm;
            heightCm = widthCm / aspect;
        }

        return (logo.Bytes, widthCm, heightCm);
    }

    private static void AppendTable(Section section, ReportSheet sheet, BrandPalette palette)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.LightGray;

        foreach (var widthCm in ColumnWidthsCm(sheet))
        {
            table.AddColumn(Unit.FromCentimeter(widthCm));
        }

        var headerRow = table.AddRow();
        headerRow.HeadingFormat = true; // repeat the header when the table breaks across pages
        headerRow.Shading.Color = ToColor(palette.TableHeaderFill);
        headerRow.Format.Font.Bold = true;
        headerRow.Format.Font.Color = ToColor(palette.TableHeaderText);
        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            headerRow.Cells[i].AddParagraph(sheet.Columns[i]);
        }

        foreach (var dataRow in sheet.Rows)
        {
            var row = table.AddRow();
            for (var i = 0; i < sheet.Columns.Count; i++)
            {
                var text = i < dataRow.Count ? dataRow[i] ?? string.Empty : string.Empty;
                var paragraph = row.Cells[i].AddParagraph(text);
                if (LongestToken(text) > LongTokenChars)
                {
                    // An unbreakable run (a SHA-256, a long reference) cannot wrap — set it smaller so it stays in its cell.
                    paragraph.Format.Font.Size = 7;
                }
            }
        }
    }

    // A "word" longer than this cannot wrap inside an ordinary column.
    private const int LongTokenChars = 30;

    /// <summary>
    /// Share the usable width between the columns by what each has to show, instead of equally. Each column is first
    /// given enough for its longest unbreakable word (a section name, a hash) — text can wrap, a word cannot — and
    /// the width left over goes to the columns with the most to say, so a long prompt gets room and a "Yes / No"
    /// column does not waste it.
    /// </summary>
    private static double[] ColumnWidthsCm(ReportSheet sheet)
    {
        // Approximate advance per character for the body face, in cm: 9pt regular, 9pt bold (headers), 7pt (long tokens).
        const double charCm = 0.185;
        const double boldCharCm = 0.2;
        const double smallCharCm = 0.145;
        const double paddingCm = 0.4;
        const double floorCm = 1.2;

        var count = Math.Max(1, sheet.Columns.Count);
        var minimum = new double[count];
        var desired = new double[count];
        for (var i = 0; i < count; i++)
        {
            var header = i < sheet.Columns.Count ? sheet.Columns[i] : string.Empty;
            var longestCell = header.Length;
            var needCm = (LongestToken(header) * boldCharCm) + paddingCm;
            foreach (var row in sheet.Rows)
            {
                var text = i < row.Count ? row[i] ?? string.Empty : string.Empty;
                longestCell = Math.Max(longestCell, text.Length);
                var token = LongestToken(text);
                needCm = Math.Max(needCm, (token * (token > LongTokenChars ? smallCharCm : charCm)) + paddingCm);
            }

            minimum[i] = Math.Max(floorCm, needCm);
            // Wrapping text reads comfortably at up to ~40 characters a line; beyond that it simply wraps.
            desired[i] = Math.Max(minimum[i], (Math.Min(longestCell, 40) * charCm) + paddingCm);
        }

        var minimumTotal = minimum.Sum();
        if (minimumTotal >= UsableWidthCm)
        {
            // More unbreakable content than the page is wide — scale everything down proportionally.
            return minimum.Select(m => m * UsableWidthCm / minimumTotal).ToArray();
        }

        var spare = UsableWidthCm - minimumTotal;
        var wanted = desired.Zip(minimum, (d, m) => d - m).ToArray();
        var wantedTotal = wanted.Sum();
        return wantedTotal <= 0
            ? minimum.Select(m => m + (spare / count)).ToArray()
            : minimum.Select((m, i) => m + (spare * wanted[i] / wantedTotal)).ToArray();
    }

    private static int LongestToken(string text)
    {
        var longest = 0;
        var current = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '/')
            {
                current = 0;
                continue;
            }

            longest = Math.Max(longest, ++current);
        }

        return longest;
    }

    private static Color ToColor(Rgb rgb) => new(rgb.R, rgb.G, rgb.B);
}
