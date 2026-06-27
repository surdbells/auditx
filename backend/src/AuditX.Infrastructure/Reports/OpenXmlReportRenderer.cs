using System.Security.Cryptography;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Reports;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// Renders the report as a DOCX via DocumentFormat.OpenXml (MIT) when requested (M8). Same composition as the
/// canonical HTML, gated by the same fixed conditional-flag evaluator; embeds no evidence bytes (references only).
/// The DOCX carries its own SHA-256 in <see cref="RenderedArtefact"/>; the CANONICAL/verifiable hash is the HTML's.
/// </summary>
public sealed class OpenXmlReportRenderer : IReportRenderer
{
    public IReadOnlyCollection<string> SupportedFormats { get; } = ["docx"];

    public bool CanRender(string format) => string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase);

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        var c = context.Composition;
        var flags = ConditionalSectionEvaluator.BuildFlags(c);
        var sections = ReportTemplateDefinition.ReadSections(context.TemplateDefinitionJson);
        var title = ReportTemplateDefinition.ReadTitle(context.TemplateDefinitionJson) ?? "Audit Report";

        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

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
        KeyValue(body, "Pass / Fail / N/A / Unanswered", $"{c.PassCount} / {c.FailCount} / {c.NaCount} / {c.UnansweredCount}");
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
    {
        var run = new Run(new RunProperties(new Bold(), new FontSize { Val = level == 1 ? "32" : "26" }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        body.AppendChild(new Paragraph(run));
    }

    private static void KeyValue(Body body, string label, string value)
    {
        var labelRun = new Run(new RunProperties(new Bold()), new Text($"{label}: ") { Space = SpaceProcessingModeValues.Preserve });
        var valueRun = new Run(new Text(value) { Space = SpaceProcessingModeValues.Preserve });
        body.AppendChild(new Paragraph(labelRun, valueRun));
    }

    private static void Paragraph(Body body, string text)
        => body.AppendChild(new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
}
