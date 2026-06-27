using System.Text.Json;
using AuditX.Domain.Common;

namespace AuditX.Domain.Reports;

/// <summary>A single section declared by a report template: a stable key, a display title, and an optional render condition.</summary>
public sealed record ReportTemplateSection(string Key, string Title, string? Condition);

/// <summary>
/// Parsing + shape validation for the report-template definition JSON (M8). The shape is
/// <c>{ "title"?: string, "sections": [ { "key": string, "title": string, "condition"?: string } ] }</c>.
/// A section's optional <c>condition</c> is a FIXED flag name evaluated against the composition's flag bag
/// (no code execution; unknown flags omit the section). Throws 422 on a malformed definition.
/// </summary>
public static class ReportTemplateDefinition
{
    /// <summary>Validate the template definition JSON has the expected shape. Throws <see cref="DomainException"/> (422) on a bad shape.</summary>
    public static void ValidateShape(string definitionJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(definitionJson);
        }
        catch (JsonException)
        {
            throw new DomainException("report_template.invalid_json", "The template definition is not valid JSON.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("sections", out var sections)
                || sections.ValueKind != JsonValueKind.Array)
            {
                throw new DomainException("report_template.invalid_shape", "The template definition must be an object with a 'sections' array.");
            }

            foreach (var section in sections.EnumerateArray())
            {
                if (section.ValueKind != JsonValueKind.Object
                    || !section.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(key.GetString()))
                {
                    throw new DomainException("report_template.invalid_section", "Each section must be an object with a non-empty 'key'.");
                }
            }
        }
    }

    /// <summary>Optional document title from the definition (falls back to a default in the renderer).</summary>
    public static string? ReadTitle(string definitionJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parse the ordered sections from the definition. Returns an empty list on a malformed definition.</summary>
    public static IReadOnlyList<ReportTemplateSection> ReadSections(string definitionJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<ReportTemplateSection>();
            foreach (var section in sections.EnumerateArray())
            {
                if (section.ValueKind != JsonValueKind.Object || !section.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var keyValue = key.GetString();
                if (string.IsNullOrWhiteSpace(keyValue))
                {
                    continue;
                }

                var title = section.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : keyValue;
                var condition = section.TryGetProperty("condition", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                result.Add(new ReportTemplateSection(keyValue, title ?? keyValue, condition));
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
