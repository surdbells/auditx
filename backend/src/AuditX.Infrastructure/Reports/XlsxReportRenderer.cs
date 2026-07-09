using System.Security.Cryptography;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Reports.Generation;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// Renders the report's tabular content as an Excel workbook via DocumentFormat.OpenXml (MIT — the same library the
/// DOCX renderer uses) (M8). Snapshot-faithful: it projects the SAME assembled composition / standalone model the
/// HTML renderer uses (via <see cref="ReportTabularProjection"/>), one worksheet per section table. All cells are
/// inline strings (values are already formatted for the report), so the file is locale-independent. Carries its own
/// SHA-256; the canonical/verifiable hash remains the HTML's.
/// </summary>
public sealed class XlsxReportRenderer : IReportRenderer
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public IReadOnlyCollection<string> SupportedFormats { get; } = ["xlsx"];

    public bool CanRender(string format) => string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        var sheets = ReportTabularProjection.From(context);

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheetsElement = workbookPart.Workbook.AppendChild(new Sheets());

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            uint sheetId = 1;
            foreach (var sheet in sheets)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(sheetData);

                sheetData.Append(BuildRow(sheet.Columns));
                foreach (var row in sheet.Rows)
                {
                    sheetData.Append(BuildRow(row));
                }

                sheetsElement.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = SafeSheetName(sheet.Name, usedNames),
                });
            }

            // Excel requires at least one sheet.
            if (sheetId == 1)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());
                sheetsElement.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Report" });
            }

            workbookPart.Workbook.Save();
        }

        var bytes = stream.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(bytes, XlsxContentType, $"{ReportArtefactNaming.Stem(context)}.xlsx", "xlsx", sha256);
    }

    private static Row BuildRow(IReadOnlyList<string> fields)
    {
        var row = new Row();
        foreach (var field in fields)
        {
            row.Append(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(field ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }),
            });
        }

        return row;
    }

    /// <summary>Excel sheet names: ≤31 chars, none of <c>[ ] : * ? / \</c>, non-empty, unique within the workbook.</summary>
    private static string SafeSheetName(string name, HashSet<string> used)
    {
        var cleaned = new string((name ?? string.Empty).Where(ch => ch is not ('[' or ']' or ':' or '*' or '?' or '/' or '\\')).ToArray()).Trim();
        if (string.IsNullOrEmpty(cleaned))
        {
            cleaned = "Sheet";
        }

        if (cleaned.Length > 31)
        {
            cleaned = cleaned[..31];
        }

        var candidate = cleaned;
        var suffix = 2;
        while (!used.Add(candidate))
        {
            var tag = $" ({suffix++})";
            candidate = cleaned.Length + tag.Length > 31 ? cleaned[..(31 - tag.Length)] + tag : cleaned + tag;
        }

        return candidate;
    }
}
