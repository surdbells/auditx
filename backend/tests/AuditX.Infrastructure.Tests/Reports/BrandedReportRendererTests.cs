using System.Text;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using AuditX.Infrastructure.Reports;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace AuditX.Infrastructure.Tests.Reports;

/// <summary>
/// Every report format carries the institution's branding, written INTO the artefact — so a report keeps the look it
/// was issued with, and two reports issued under different branding are different documents.
/// </summary>
public sealed class BrandedReportRendererTests
{
    private const string Png1X1Base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    private const string PngLogo = "data:image/png;base64," + Png1X1Base64;
    private const string SvgLogo = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=";

    private static readonly ReportBranding Alpha = ReportBranding.Create("Alpha Bank", "#0a5c36", "#c9a227", PngLogo);
    private static readonly ReportBranding Beta = ReportBranding.Create("Beta Bank", "#b00020", "#333333", null);

    private static ReportComposition Composition() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "Branch audit", "branch", "under_review", "All tellers",
        new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 10), null, VersionNumber: 1,
        ReportContentAssembler.Sha256Placeholder, TotalItems: 2, PassCount: 1, FailCount: 0, NaCount: 0, UnansweredCount: 1,
        ExceptionCount: 0, CriticalExceptionCount: 0, RecurrenceCount: 0,
        ChecklistLines:
        [
            new ReportChecklistLine(Guid.NewGuid(), "Is the vault dual-controlled?", "Cash", "pass", null, "Compliant"),
            new ReportChecklistLine(Guid.NewGuid(), "Days since last reconciliation", "Cash", null, null, "12.5 days"),
        ],
        Exceptions: [], EvidenceReferences: [], GeneratedAtUtc: new DateTimeOffset(2027, 2, 11, 9, 0, 0, TimeSpan.Zero));

    private static StandaloneReportModel Standalone() => new(
        ReportKind.ExecutiveSummary, "Executive Summary", "Cross-audit overview", 3,
        new DateTimeOffset(2027, 2, 11, 9, 0, 0, TimeSpan.Zero),
        [
            new StandaloneSection("Portfolio", [new StandaloneMetric("Audits", "12")],
                new StandaloneTable(["Audit", "Status"], [["Branch audit", "Completed"]])),
        ]);

    private static ReportRenderContext Engagement(ReportBranding branding)
        => ReportRenderContext.ForEngagement(Composition(), "{\"title\":\"Audit Report\"}", 1, branding);

    private static ReportRenderContext StandaloneContext(ReportBranding branding)
        => ReportRenderContext.ForStandalone(Standalone(), "{\"title\":\"Executive Summary\"}", 1, branding);

    private static string HtmlOf(ReportRenderContext context)
        => Encoding.UTF8.GetString(new HtmlReportRenderer().Render("html", context).Content);

    // ---- HTML (the canonical, hashed artefact) -----------------------------------------------

    [Fact]
    public void Html_carries_the_logo_name_and_colours_inline()
    {
        var html = HtmlOf(Engagement(Alpha));

        Assert.Contains("Alpha Bank", html);
        Assert.Contains($"src=\"{PngLogo}\"", html); // embedded — nothing is fetched when the report is opened
        Assert.Contains("#0a5c36", html);
        Assert.Contains("#c9a227", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public void Standalone_html_is_branded_the_same_way()
    {
        var html = HtmlOf(StandaloneContext(Alpha));

        Assert.Contains("Alpha Bank", html);
        Assert.Contains($"src=\"{PngLogo}\"", html);
        Assert.Contains("#0a5c36", html);
    }

    [Fact]
    public void Html_without_a_logo_still_names_the_institution()
    {
        var html = HtmlOf(Engagement(Beta));

        Assert.Contains("Beta Bank", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void The_same_report_under_different_branding_is_a_different_hashed_document()
    {
        var renderer = new HtmlReportRenderer();

        var alpha = renderer.Render("html", Engagement(Alpha));
        var alphaAgain = renderer.Render("html", Engagement(Alpha));
        var beta = renderer.Render("html", Engagement(Beta));

        // Deterministic for the same input, so the stored hash is reproducible…
        Assert.Equal(alpha.Sha256Hash, alphaAgain.Sha256Hash);
        // …and the branding is part of what is hashed: a rebrand cannot be slipped into an issued report unnoticed.
        Assert.NotEqual(alpha.Sha256Hash, beta.Sha256Hash);
    }

    [Fact]
    public void The_institution_name_is_html_encoded()
    {
        var html = HtmlOf(Engagement(ReportBranding.Create("<script>alert(1)</script> & Co", null, null, null)));

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void A_pale_brand_colour_still_yields_readable_headings()
    {
        var palette = new BrandPalette(ReportBranding.Create("Sunny Bank", "#ffee58", "#fff59d", null));

        // The band keeps the true brand colour; heading text is darkened until it reads on white.
        Assert.Equal("#ffee58", palette.Primary.Hex);
        Assert.True(palette.HeadingText.ContrastWith(Rgb.White) >= 4.5);
        Assert.True(palette.TableHeaderText.ContrastWith(palette.TableHeaderFill) >= 4.5);
    }

    // ---- PDF -----------------------------------------------------------------------------------

    [Fact]
    public void Pdf_renders_with_a_png_logo()
    {
        var withLogo = new PdfReportRenderer().Render("pdf", Engagement(Alpha));
        var withoutLogo = new PdfReportRenderer().Render("pdf", Engagement(Beta));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(withLogo.Content, 0, 4));
        Assert.Contains("/Subtype/Image", Latin1(withLogo.Content).Replace(" ", string.Empty));
        Assert.DoesNotContain("/Subtype/Image", Latin1(withoutLogo.Content).Replace(" ", string.Empty));
    }

    [Fact]
    public void Pdf_omits_a_logo_it_cannot_embed_instead_of_failing()
    {
        // SVG is fine in the HTML report but not embeddable by the PDF engine — the report must still be produced.
        var artefact = new PdfReportRenderer().Render("pdf", Engagement(ReportBranding.Create("Vector Bank", "#0a5c36", "#c9a227", SvgLogo)));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(artefact.Content, 0, 4));
    }

    [Fact]
    public void Pdf_survives_a_logo_that_claims_to_be_a_png_but_is_not()
    {
        var bogus = "data:image/png;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes("definitely not an image"));

        var artefact = new PdfReportRenderer().Render("pdf", Engagement(ReportBranding.Create("Odd Bank", null, null, bogus)));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(artefact.Content, 0, 4));
    }

    // ---- Word ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Docx_is_schema_valid_and_embeds_the_logo_when_there_is_one(bool standalone)
    {
        var context = standalone ? StandaloneContext(Alpha) : Engagement(Alpha);

        var artefact = new OpenXmlReportRenderer().Render("docx", context);

        using var document = WordprocessingDocument.Open(new MemoryStream(artefact.Content), false);
        var errors = new OpenXmlValidator().Validate(document).Select(e => $"{e.Path?.XPath}: {e.Description}").ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors)); // Word refuses invalid documents
        Assert.Single(document.MainDocumentPart!.ImageParts);
        Assert.Contains("Alpha Bank", document.MainDocumentPart.Document!.InnerText);
        Assert.Contains("0A5C36", document.MainDocumentPart.StyleDefinitionsPart!.Styles!.OuterXml);
    }

    [Fact]
    public void Docx_without_an_embeddable_logo_is_still_valid_and_named()
    {
        var artefact = new OpenXmlReportRenderer().Render("docx", Engagement(ReportBranding.Create("Vector Bank", "#0a5c36", "#c9a227", SvgLogo)));

        using var document = WordprocessingDocument.Open(new MemoryStream(artefact.Content), false);
        Assert.Empty(new OpenXmlValidator().Validate(document));
        Assert.Empty(document.MainDocumentPart!.ImageParts);
        Assert.Contains("Vector Bank", document.MainDocumentPart.Document!.InnerText);
    }

    // ---- Excel / CSV (data exports) ------------------------------------------------------------

    [Fact]
    public void Xlsx_is_schema_valid_with_brand_coloured_headers_and_names_the_institution()
    {
        var artefact = new XlsxReportRenderer().Render("xlsx", Engagement(Alpha));

        using var document = SpreadsheetDocument.Open(new MemoryStream(artefact.Content), false);
        Assert.Empty(new OpenXmlValidator().Validate(document));
        var styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!.OuterXml;
        Assert.Contains(new BrandPalette(Alpha).TableHeaderFill.HexNoHash, styles);
        var firstSheet = document.WorkbookPart.WorksheetParts.First().Worksheet!.InnerText;
        Assert.Contains("Alpha Bank", string.Concat(document.WorkbookPart.WorksheetParts.Select(p => p.Worksheet!.InnerText)));
        Assert.NotEmpty(firstSheet);
    }

    [Fact]
    public void Csv_names_the_institution_and_carries_what_was_answered()
    {
        var csv = Encoding.UTF8.GetString(new CsvReportRenderer().Render("csv", Engagement(Alpha)).Content);

        Assert.Contains("Institution,Alpha Bank", csv);
        Assert.Contains("Section,Prompt,Answer,Verdict,Comment", csv);
        Assert.Contains("Compliant", csv); // the conclusion as the responder saw it
        Assert.Contains("12.5 days", csv); // a value-type answer is no longer dropped from the export
    }

    // ---- Logo geometry -------------------------------------------------------------------------

    [Fact]
    public void ImageHeader_reads_png_and_jpeg_dimensions_and_rejects_other_data()
    {
        Assert.True(ImageHeader.TryRead(Convert.FromBase64String(Png1X1Base64), out var kind, out var w, out var h));
        Assert.Equal(ImageHeader.Kind.Png, kind);
        Assert.Equal((1, 1), (w, h));

        // Minimal JPEG header: SOI, an APP0 segment, then SOF0 declaring 32 x 16.
        byte[] jpeg =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
            0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x10, 0x00, 0x20, 0x01, 0x01, 0x11, 0x00,
        ];
        Assert.True(ImageHeader.TryRead(jpeg, out kind, out w, out h));
        Assert.Equal(ImageHeader.Kind.Jpeg, kind);
        Assert.Equal((32, 16), (w, h));

        Assert.False(ImageHeader.TryRead(Encoding.ASCII.GetBytes("<svg/>"), out _, out _, out _));
        Assert.False(ImageHeader.TryRead([], out _, out _, out _));
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
