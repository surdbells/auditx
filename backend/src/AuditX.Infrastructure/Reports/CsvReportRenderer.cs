using System.Security.Cryptography;
using System.Text;
using AuditX.Application.Abstractions.Reports;
using AuditX.Application.Common.Csv;
using AuditX.Application.Reports.Generation;
using AuditX.Domain.Enums;

namespace AuditX.Infrastructure.Reports;

/// <summary>
/// Renders the report's tabular content as CSV (M8). Snapshot-faithful — it projects the SAME assembled
/// composition / standalone model the HTML renderer uses (via <see cref="ReportTabularProjection"/>), so the export
/// matches the report as issued. A multi-table report is emitted as consecutive RFC-4180 blocks (a block-title line,
/// a header row, then the data rows) separated by a blank line. Carries its own SHA-256.
/// </summary>
public sealed class CsvReportRenderer : IReportRenderer
{
    public IReadOnlyCollection<string> SupportedFormats { get; } = ["csv"];

    public bool CanRender(string format) => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    public RenderedArtefact Render(string format, ReportRenderContext context)
    {
        var sheets = ReportTabularProjection.From(context);

        var sb = new StringBuilder();
        for (var i = 0; i < sheets.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            var sheet = sheets[i];
            sb.Append(CsvWriter.Field(sheet.Name)).Append('\n');
            AppendLine(sb, sheet.Columns);
            foreach (var row in sheet.Rows)
            {
                AppendLine(sb, row);
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new RenderedArtefact(bytes, "text/csv; charset=utf-8", $"{ReportArtefactNaming.Stem(context)}.csv", "csv", sha256);
    }

    private static void AppendLine(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(CsvWriter.Field(fields[i]));
        }

        sb.Append('\n');
    }
}
