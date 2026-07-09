using AuditX.Application.Abstractions.Reports;
using AuditX.Domain.Enums;

namespace AuditX.Application.Reports.Generation;

/// <summary>
/// A single tabular block of a report — a named grid with a header row and zero or more data rows. It is the
/// common shape the CSV and XLSX renderers consume: CSV concatenates the sheets as blocks; XLSX renders one
/// worksheet per sheet (M8).
/// </summary>
public sealed record ReportSheet(string Name, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Projects a <see cref="ReportRenderContext"/> into a list of <see cref="ReportSheet"/> — the tabular view of the
/// report used for the CSV / Excel exports (M8). Snapshot-faithful: it reads the SAME assembled composition /
/// standalone model the HTML + DOCX renderers use, so the exports match the report as issued. Handles both the
/// per-audit engagement shape and the cross-audit standalone shape.
/// </summary>
public static class ReportTabularProjection
{
    public static IReadOnlyList<ReportSheet> From(ReportRenderContext context)
        => context.Kind == ReportKind.AuditEngagement && context.Composition is { } composition
            ? FromEngagement(composition)
            : context.Standalone is { } standalone
                ? FromStandalone(standalone)
                : [];

    private static IReadOnlyList<ReportSheet> FromEngagement(ReportComposition c)
    {
        var sheets = new List<ReportSheet>
        {
            new("Summary", ["Metric", "Value"],
            [
                Kv("Audit", c.AuditName),
                Kv("Audit type", c.AuditType),
                Kv("Status", c.AuditStatus),
                Kv("Report version", c.VersionNumber.ToString()),
                Kv("Conducted", $"{c.StartDate:yyyy-MM-dd} to {(c.ActualEndDate ?? c.TargetEndDate):yyyy-MM-dd}"),
                Kv("Generated (UTC)", c.GeneratedAtUtc.ToString("u")),
                Kv("Checklist items", c.TotalItems.ToString()),
                Kv("Pass", c.PassCount.ToString()),
                Kv("Fail", c.FailCount.ToString()),
                Kv("N/A", c.NaCount.ToString()),
                Kv("Unanswered", c.UnansweredCount.ToString()),
                Kv("Exceptions raised", c.ExceptionCount.ToString()),
                Kv("Critical exceptions", c.CriticalExceptionCount.ToString()),
                Kv("Recurrence flags", c.RecurrenceCount.ToString()),
            ]),
        };

        if (c.Exceptions.Count > 0)
        {
            sheets.Add(new ReportSheet("Findings",
                ["Title", "Severity", "Category", "Status", "Recurrence", "MAP completed", "MAP total"],
                c.Exceptions.Select(e => (IReadOnlyList<string>)
                [
                    e.Title, e.Severity, e.Category ?? "", e.Status, e.IsRecurrence ? "Yes" : "No",
                    e.CompletedMapActionCount.ToString(), e.MapActionCount.ToString(),
                ]).ToArray()));
        }

        if (c.ChecklistLines.Count > 0)
        {
            sheets.Add(new ReportSheet("Checklist",
                ["Section", "Prompt", "Verdict", "Comment"],
                c.ChecklistLines.Select(l => (IReadOnlyList<string>)
                [
                    l.SectionName ?? "", l.Prompt, l.Verdict ?? "", l.Comment ?? "",
                ]).ToArray()));
        }

        if (c.EvidenceReferences.Count > 0)
        {
            sheets.Add(new ReportSheet("Evidence",
                ["File", "SHA-256", "Size (bytes)"],
                c.EvidenceReferences.Select(r => (IReadOnlyList<string>)
                [
                    r.Filename, r.Sha256Hash, r.SizeBytes.ToString(),
                ]).ToArray()));
        }

        return sheets;
    }

    private static IReadOnlyList<ReportSheet> FromStandalone(StandaloneReportModel model)
    {
        var sheets = new List<ReportSheet>();

        // A single overview sheet gathers every section's key/value metrics + notes.
        var overview = new List<IReadOnlyList<string>>
        {
            Row3(model.Title, "Report version", model.VersionNumber.ToString()),
            Row3(model.Title, "Generated (UTC)", model.GeneratedAtUtc.ToString("u")),
        };
        foreach (var section in model.Sections)
        {
            foreach (var metric in section.Metrics)
            {
                overview.Add(Row3(section.Heading, metric.Label, metric.Value));
            }

            if (section.Metrics.Count == 0 && !string.IsNullOrWhiteSpace(section.Note))
            {
                overview.Add(Row3(section.Heading, "", section.Note));
            }
        }

        sheets.Add(new ReportSheet("Summary", ["Section", "Metric", "Value"], overview));

        // Each section that carries a table becomes its own sheet.
        foreach (var section in model.Sections)
        {
            if (section.Table is { } table && table.Columns.Count > 0)
            {
                sheets.Add(new ReportSheet(section.Heading, table.Columns, table.Rows));
            }
        }

        return sheets;
    }

    private static IReadOnlyList<string> Kv(string label, string value) => [label, value];

    private static IReadOnlyList<string> Row3(string a, string b, string c) => [a, b, c];
}
