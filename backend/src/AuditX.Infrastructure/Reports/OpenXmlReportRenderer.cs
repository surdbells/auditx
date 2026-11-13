using System.Security.Cryptography;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Reports;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// Renders the report as a DOCX via DocumentFormat.OpenXml (MIT) when requested (M8). Same composition as the
/// canonical HTML, gated by the same fixed conditional-flag evaluator; embeds no evidence bytes (references only).
/// The DOCX carries its own SHA-256 in <see cref="RenderedArtefact"/>; the CANONICAL/verifiable hash is the HTML's.
/// Branded like the other formats: the institution's logo (PNG / JPEG) and name as a letterhead, headings in its
/// colours — all embedded in the file, so the document keeps the look it was issued with.
/// </summary>
public sealed class OpenXmlReportRenderer : IReportRenderer
{
    private const string OrgStyleId = "BrandOrganisation";
    private const string Heading1StyleId = "BrandHeading1";
    private const string Heading2StyleId = "BrandHeading2";

    // The logo is placed at most this tall / wide (English Metric Units: 914,400 per inch), keeping its aspect ratio.
    private const long LogoMaxHeightEmu = 502_920; // 0.55 in
    private const long LogoMaxWidthEmu = 1_828_800; // 2.0 in

    public IReadOnlyCollection<string> SupportedFormats { get; } = ["docx"];

    public bool CanRender(string format) => string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase);

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        if (context.Kind != Domain.Enums.ReportKind.AuditEngagement && context.Standalone is { } standalone)
        {
            return RenderStandalone(standalone, context.Branding);
        }

        var c = context.Composition!;
        var flags = ConditionalSectionEvaluator.BuildFlags(c);
        var sections = ReportTemplateDefinition.ReadSections(context.TemplateDefinitionJson);
        var title = ReportTemplateDefinition.ReadTitle(context.TemplateDefinitionJson) ?? "Audit Report";

        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var palette = new BrandPalette(context.Branding);
            AddBrandStyles(mainPart, palette);
            AppendLetterhead(mainPart, body, context.Branding);

            Heading(body, title, 1);
            KeyValue(body, "Audit", c.AuditName);
            KeyValue(body, "Audit type", c.AuditType);
            KeyValue(body, "Status", c.AuditStatus);
            KeyValue(body, "Report version", c.VersionNumber.ToString());
            KeyValue(body, "Conducted", $"{c.StartDate:yyyy-MM-dd} to {(c.ActualEndDate ?? c.TargetEndDate):yyyy-MM-dd}");
            KeyValue(body, "Generated (UTC)", c.GeneratedAtUtc.ToString("u"));

            if (sections.Count == 0)
            {
                AppendExecutiveSummary(body, c);
                AppendScope(body, c);
                AppendFindings(body, c);
                AppendEvidence(body, c);
            }
            else
            {
                foreach (var section in sections)
                {
                    if (!ConditionalSectionEvaluator.ShouldRender(section.Condition, flags))
                    {
                        continue;
                    }

                    AppendSection(body, section, c);
                }
            }

            mainPart.Document.Save();
        }

        var bytes = stream.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(
            bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"audit-report-{c.AuditId}-v{c.VersionNumber}.docx",
            "docx",
            sha256);
    }

    /// <summary>Renders a standalone (cross-audit) report from its generic section model (M8).</summary>
    private static RenderedArtefact RenderStandalone(StandaloneReportModel model, ReportBranding branding)
    {
        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var palette = new BrandPalette(branding);
            AddBrandStyles(mainPart, palette);
            AppendLetterhead(mainPart, body, branding);

            Heading(body, model.Title, 1);
            Paragraph(body, model.Subtitle);
            KeyValue(body, "Report version", model.VersionNumber.ToString());
            KeyValue(body, "Generated (UTC)", model.GeneratedAtUtc.ToString("u"));

            foreach (var section in model.Sections)
            {
                Heading(body, section.Heading, 2);
                foreach (var metric in section.Metrics)
                {
                    KeyValue(body, metric.Label, metric.Value);
                }

                if (section.Table is { } table && table.Columns.Count > 0)
                {
                    AppendTable(body, table, palette);
                }

                if (!string.IsNullOrWhiteSpace(section.Note))
                {
                    Paragraph(body, section.Note!);
                }
            }

            mainPart.Document.Save();
        }

        var bytes = stream.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(
            bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"{model.FileStem}-v{model.VersionNumber}.docx",
            "docx",
            sha256);
    }

    private static void AppendTable(Body body, StandaloneTable model, BrandPalette palette)
    {
        // Schema order matters to Word: borders are top, left, bottom, right, then the inside rules; and a table
        // declares its grid (one column per header) before its rows.
        var table = new Table(new TableProperties(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));
        table.AppendChild(new TableGrid(model.Columns.Select(_ => new GridColumn())));

        var header = new TableRow();
        foreach (var column in model.Columns)
        {
            header.AppendChild(Cell(column, bold: true, palette.TableHeaderFill.HexNoHash, palette.TableHeaderText.HexNoHash));
        }

        table.AppendChild(header);

        foreach (var dataRow in model.Rows)
        {
            var row = new TableRow();
            foreach (var cell in dataRow)
            {
                row.AppendChild(Cell(cell ?? string.Empty, bold: false));
            }

            table.AppendChild(row);
        }

        body.AppendChild(table);
    }

    private static TableCell Cell(string text, bool bold, string? fillHex = null, string? textHex = null)
    {
        var runProps = new RunProperties();
        if (bold)
        {
            runProps.AppendChild(new Bold());
        }

        if (textHex is not null)
        {
            runProps.AppendChild(new Color { Val = textHex });
        }

        var cell = new TableCell();
        if (fillHex is not null)
        {
            cell.AppendChild(new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fillHex }));
        }

        cell.AppendChild(new Paragraph(new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        return cell;
    }

    /// <summary>
    /// Defines the three paragraph styles the report uses, carrying the brand colours — so every heading picks the
    /// colours up from the style rather than each call site having to know the palette.
    /// </summary>
    private static void AddBrandStyles(MainDocumentPart mainPart, BrandPalette palette)
    {
        var heading = palette.HeadingText.HexNoHash;
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(
            ParagraphStyle(OrgStyleId, "Brand Organisation", heading, halfPoints: "28", spaceBefore: "0", spaceAfter: "240",
                borderHex: palette.Primary.HexNoHash, borderEighths: 18U),
            ParagraphStyle(Heading1StyleId, "Brand Heading 1", heading, halfPoints: "32", spaceBefore: "120", spaceAfter: "120",
                borderHex: null, borderEighths: 0U),
            ParagraphStyle(Heading2StyleId, "Brand Heading 2", heading, halfPoints: "26", spaceBefore: "280", spaceAfter: "100",
                borderHex: palette.Accent.HexNoHash, borderEighths: 8U));
        stylesPart.Styles.Save();
    }

    private static Style ParagraphStyle(
        string id, string name, string colorHex, string halfPoints, string spaceBefore, string spaceAfter, string? borderHex, uint borderEighths)
    {
        // Child order follows the schema (keepNext, pBdr, spacing / b, color, sz) — Word rejects out-of-order properties.
        var paragraph = new StyleParagraphProperties(new KeepNext());
        if (borderHex is not null)
        {
            paragraph.AppendChild(new ParagraphBorders(
                new BottomBorder { Val = BorderValues.Single, Size = borderEighths, Space = 3U, Color = borderHex }));
        }

        paragraph.AppendChild(new SpacingBetweenLines { Before = spaceBefore, After = spaceAfter });

        return new Style(
            new StyleName { Val = name },
            paragraph,
            new StyleRunProperties(new Bold(), new Color { Val = colorHex }, new FontSize { Val = halfPoints }))
        {
            Type = StyleValues.Paragraph,
            StyleId = id,
            CustomStyle = true,
        };
    }

    /// <summary>
    /// The letterhead: the logo (when it is a PNG or JPEG — the image types Word embeds natively) above the
    /// institution's name. A logo in another format is omitted; the name and colours still brand the document.
    /// </summary>
    private static void AppendLetterhead(MainDocumentPart mainPart, Body body, ReportBranding branding)
    {
        if (branding.DecodeLogo() is { } logo
            && ImageHeader.TryRead(logo.Bytes, out var kind, out var width, out var height))
        {
            var imagePart = mainPart.AddImagePart(kind == ImageHeader.Kind.Png ? ImagePartType.Png : ImagePartType.Jpeg);
            using (var stream = new MemoryStream(logo.Bytes))
            {
                imagePart.FeedData(stream);
            }

            var aspect = (double)width / height;
            var cy = LogoMaxHeightEmu;
            var cx = (long)Math.Round(cy * aspect);
            if (cx > LogoMaxWidthEmu)
            {
                cx = LogoMaxWidthEmu;
                cy = (long)Math.Round(cx / aspect);
            }

            body.AppendChild(new Paragraph(new Run(InlinePicture(mainPart.GetIdOfPart(imagePart), cx, cy, $"{branding.InstitutionName} logo"))));
        }

        body.AppendChild(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = OrgStyleId }),
            new Run(new Text(branding.InstitutionName) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static Drawing InlinePicture(string relationshipId, long cx, long cy, string description)
        => new(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "Logo", Description = description },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });

    private static void AppendSection(Body body, ReportTemplateSection section, ReportComposition c)
    {
        switch (section.Key)
        {
            case "executive_summary":
                AppendExecutiveSummary(body, c, section.Title);
                break;
            case "scope":
            case "methodology":
                AppendScope(body, c, section.Title);
                break;
            case "findings":
                AppendFindings(body, c, section.Title);
                break;
            case "evidence":
            case "appendix":
                AppendEvidence(body, c, section.Title);
                break;
            default:
                Heading(body, section.Title, 2);
                break;
        }
    }

    private static void AppendExecutiveSummary(Body body, ReportComposition c, string heading = "Executive summary")
    {
        Heading(body, heading, 2);
        KeyValue(body, "Checklist items", c.TotalItems.ToString());
        KeyValue(body, "Pass / Fail / N/A", $"{c.PassCount} / {c.FailCount} / {c.NaCount}");
        KeyValue(body, "Answered without a conclusion", c.RecordedCount.ToString());
        KeyValue(body, "Unanswered", c.UnansweredCount.ToString());
        KeyValue(body, "Exceptions raised", c.ExceptionCount.ToString());
        KeyValue(body, "Critical exceptions", c.CriticalExceptionCount.ToString());
        KeyValue(body, "Recurrence flags", c.RecurrenceCount.ToString());
    }

    private static void AppendScope(Body body, ReportComposition c, string heading = "Scope")
    {
        Heading(body, heading, 2);
        Paragraph(body, string.IsNullOrWhiteSpace(c.ScopeDescription) ? "—" : c.ScopeDescription);
    }

    private static void AppendFindings(Body body, ReportComposition c, string heading = "Findings")
    {
        Heading(body, heading, 2);
        if (c.Exceptions.Count == 0)
        {
            Paragraph(body, "No exceptions were raised.");
            return;
        }

        foreach (var e in c.Exceptions)
        {
            Paragraph(body, $"{e.Title} — {e.Severity} ({e.Category ?? "uncategorised"}) — {e.Status} — recurrence: {(e.IsRecurrence ? "yes" : "no")} — MAP {e.CompletedMapActionCount}/{e.MapActionCount}");
        }
    }

    private static void AppendEvidence(Body body, ReportComposition c, string heading = "Evidence references (SHA-256)")
    {
        Heading(body, heading, 2);
        if (c.EvidenceReferences.Count == 0)
        {
            Paragraph(body, "No evidence files are linked to this audit.");
            return;
        }

        foreach (var reference in c.EvidenceReferences)
        {
            Paragraph(body, $"{reference.Filename} — {reference.Sha256Hash} — {reference.SizeBytes} bytes");
        }
    }

    private static void Heading(Body body, string text, int level)
        => body.AppendChild(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = level == 1 ? Heading1StyleId : Heading2StyleId }),
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));

    private static void KeyValue(Body body, string label, string value)
    {
        var labelRun = new Run(new RunProperties(new Bold()), new Text($"{label}: ") { Space = SpaceProcessingModeValues.Preserve });
        var valueRun = new Run(new Text(value) { Space = SpaceProcessingModeValues.Preserve });
        body.AppendChild(new Paragraph(labelRun, valueRun));
    }

    private static void Paragraph(Body body, string text)
        => body.AppendChild(new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
}
