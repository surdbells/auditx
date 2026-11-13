namespace AuditX.Application.Common.Csv;

/// <summary>Minimal RFC-4180-style CSV reader (handles quoted fields and escaped quotes; no embedded newlines).</summary>
public static class CsvReader
{
    /// <summary>The header names of a CSV document, trimmed and lower-cased (empty when there is no header row).</summary>
    public static IReadOnlyList<string> Headers(string content)
    {
        var firstLine = Normalise(content).Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstLine is null ? [] : SplitLine(firstLine).Select(h => h.Trim().ToLowerInvariant()).ToArray();
    }

    /// <summary>
    /// Line endings to LF, and the UTF-8 byte-order mark dropped: Excel writes one at the start of a "CSV UTF-8" file,
    /// which would otherwise become part of the first header name and make that column unrecognisable.
    /// </summary>
    private static string Normalise(string content)
        => (content ?? string.Empty).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');

    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string content)
    {
        var lines = Normalise(content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return [];
        }

        var headers = SplitLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (var i = 1; i < lines.Length; i++)
        {
            var fields = SplitLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++)
            {
                row[headers[c]] = c < fields.Count ? fields[c].Trim() : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<string> SplitLine(string line)
    {
        var result = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        result.Add(field.ToString());
        return result;
    }
}
