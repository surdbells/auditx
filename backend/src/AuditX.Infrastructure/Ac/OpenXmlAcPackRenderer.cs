using System.Security.Cryptography;
using AuditX.Application.Abstractions.Ac;
using AuditX.Application.Ac.Generation;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuditX.Infrastructure.Ac;

/// <summary>
/// Renders the AC pack as a DOCX via DocumentFormat.OpenXml (MIT) when requested (M13). Same composition as the
/// canonical HTML; sanctions are aggregate-only (no subject identity). The DOCX carries its own SHA-256; the
/// CANONICAL/verifiable hash is the HTML's.
/// </summary>
public sealed class OpenXmlAcPackRenderer : IAcPackRenderer
{
    public IReadOnlyCollection<string> SupportedFormats { get; } = ["docx"];

    public bool CanRender(string format) => string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase);

    public AcRenderedArtefact Render(string format, AcPackRenderContext context)
    {
        var c = context.Composition;

        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            Heading(body, "Audit Committee Pack", 1);
            KeyValue(body, "Pack version", c.VersionNumber.ToString());
            KeyValue(body, "Reporting period", $"{c.PeriodStart:yyyy-MM-dd} to {c.PeriodEnd:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(c.AcMeetingLabel))
            {
                KeyValue(body, "Meeting", c.AcMeetingLabel);
            }

            KeyValue(body, "Generated (UTC)", c.GeneratedAtUtc.ToString("u"));

            if (!string.IsNullOrWhiteSpace(context.CiaSupplementaryText))
            {
                Heading(body, "Chief Internal Auditor narrative", 2);
                Paragraph(body, context.CiaSupplementaryText);
            }

            Heading(body, "Plan status", 2);
            KeyValue(body, "Plans", c.TotalPlans.ToString());
            KeyValue(body, "Plan items", c.PlanItemsTotal.ToString());
            KeyValue(body, "Completed", c.PlanItemsCompleted.ToString());
            KeyValue(body, "Completion", $"{c.PlanCompletionPercent}%");

            Heading(body, "Exception portfolio", 2);
            KeyValue(body, "Open exceptions", c.OpenExceptionTotal.ToString());
            KeyValue(body, "Average closure (days)", c.AverageClosureDays?.ToString("0.0") ?? "—");
            foreach (var s in c.ExceptionsBySeverity)
            {
                Paragraph(body, $"{s.Severity}: {s.Count}");
            }

            Heading(body, "Material findings", 2);
            if (c.MaterialFindings.Count == 0)
            {
                Paragraph(body, "No Critical or High open findings.");
            }
            else
            {
                foreach (var f in c.MaterialFindings)
                {
                    Paragraph(body, $"{f.Title} — {f.Severity} — {f.Status} — raised {f.RaisedAt:yyyy-MM-dd} — target {f.TargetDate:yyyy-MM-dd}");
                }
            }

            Heading(body, "Sanctions consistency (aggregate)", 2);
            KeyValue(body, "Total cases", c.SanctionsTotalCases.ToString());
            KeyValue(body, "Grid adherence", $"{c.SanctionsGridAdherencePercent}%");
            KeyValue(body, "Appeal rate", $"{c.SanctionsAppealRatePercent}%");
            foreach (var r in c.SanctionsByBusinessUnit)
            {
                Paragraph(body, $"{r.BusinessUnit}: {r.CaseCount} cases, {r.GridAdherencePercent}% adherence, {r.DeviationCount} deviations, {r.AppealCount} appeals");
            }

            Heading(body, "Recurrence clusters", 2);
            if (c.RecurrenceClusters.Count == 0)
            {
                Paragraph(body, "No recurring control weaknesses detected.");
            }
            else
            {
                foreach (var rc in c.RecurrenceClusters)
                {
                    Paragraph(body, $"Entity {rc.AuditableEntityId} — {rc.Category ?? "uncategorised"} — {rc.ClosedExceptionCount} closed exceptions within {rc.WindowMonths} months");
                }
            }

            mainPart.Document.Save();
        }

        var bytes = stream.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new AcRenderedArtefact(
            bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"ac-pack-v{c.VersionNumber}.docx",
            "docx",
            sha256);
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
