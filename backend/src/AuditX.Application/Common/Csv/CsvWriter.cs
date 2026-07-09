using System.Security.Cryptography;
using System.Text;

namespace AuditX.Application.Common.Csv;

/// <summary>
/// Minimal RFC-4180 CSV builder shared by the tabular exporters (audit trail, finding register, …). Emits an
/// explicit <c>'\n'</c> after every line (header + rows) so the byte stream — and thus the SHA-256 integrity hash —
/// is identical regardless of host OS (<c>StringBuilder.AppendLine</c> is platform-dependent). The first
/// row is the header.
/// </summary>
public sealed class CsvWriter
{
    private readonly StringBuilder _sb = new();
    private int _dataRows;

    public CsvWriter(params string[] header) => AppendRaw(header);

    /// <summary>Appends a data row (counts toward <see cref="RowCount"/>).</summary>
    public void AppendRow(params string?[] fields)
    {
        AppendRaw(fields);
        _dataRows++;
    }

    /// <summary>The number of data rows appended (excludes the header).</summary>
    public int RowCount => _dataRows;

    public CsvExportResult Build(string fileName, string contentType = "text/csv")
    {
        var bytes = Encoding.UTF8.GetBytes(_sb.ToString());
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new CsvExportResult(fileName, contentType, sha, _dataRows, bytes);
    }

    private void AppendRaw(IReadOnlyList<string?> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                _sb.Append(',');
            }

            _sb.Append(Field(fields[i]));
        }

        _sb.Append('\n');
    }

    /// <summary>RFC-4180 field escaping: quote + double inner quotes when the value contains a comma/quote/newline.</summary>
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
