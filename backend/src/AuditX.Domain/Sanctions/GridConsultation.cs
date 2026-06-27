using System.Text.Json;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Sanctions;

/// <summary>The result of consulting the grid: the recommended range for the cell (null if no cell matched).</summary>
public sealed record GridConsultationResult(string? RecommendedRange)
{
    public bool CellFound => RecommendedRange is not null;
}

/// <summary>
/// Pure domain service (framework-agnostic) over the grid JSON. Given a grid definition and a
/// <c>(category, severity, isRecurrence)</c> key, it returns the recommended range and a
/// <see cref="WithinRange"/> predicate. The cell key is <c>"&lt;category&gt;|&lt;severity&gt;|&lt;true|false&gt;"</c>
/// with the category lower-cased and the severity rendered snake_case, matching the seed schema (E3).
/// </summary>
public static class GridConsultation
{
    /// <summary>Build the canonical cell key for a consultation tuple.</summary>
    public static string CellKey(string? category, ExceptionSeverity severity, bool isRecurrence)
        => $"{(category ?? string.Empty).Trim().ToLowerInvariant()}|{SnakeSeverity(severity)}|{(isRecurrence ? "true" : "false")}";

    /// <summary>Validate that the grid JSON has the expected shape (an object with a <c>cells</c> object). Throws 422 on a bad shape.</summary>
    public static void ValidateShape(string gridDefinitionJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(gridDefinitionJson);
        }
        catch (JsonException)
        {
            throw new DomainException("sanctions.grid_invalid_json", "The grid definition is not valid JSON.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Object)
            {
                throw new DomainException("sanctions.grid_invalid_schema", "The grid definition must be an object with a 'cells' object.");
            }

            foreach (var cell in cells.EnumerateObject())
            {
                if (cell.Value.ValueKind != JsonValueKind.Object
                    || !cell.Value.TryGetProperty("recommended_range", out var range)
                    || range.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(range.GetString()))
                {
                    throw new DomainException("sanctions.grid_invalid_cell", $"Grid cell '{cell.Name}' must carry a non-empty 'recommended_range' string.");
                }
            }
        }
    }

    /// <summary>Look up the recommended range for a tuple. Returns a result whose range is null when no cell matches (unknown category).</summary>
    public static GridConsultationResult Consult(string gridDefinitionJson, string? category, ExceptionSeverity severity, bool isRecurrence)
    {
        var key = CellKey(category, severity, isRecurrence);
        try
        {
            using var doc = JsonDocument.Parse(gridDefinitionJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("cells", out var cells)
                && cells.ValueKind == JsonValueKind.Object
                && cells.TryGetProperty(key, out var cell)
                && cell.ValueKind == JsonValueKind.Object
                && cell.TryGetProperty("recommended_range", out var range)
                && range.ValueKind == JsonValueKind.String)
            {
                return new GridConsultationResult(range.GetString());
            }
        }
        catch (JsonException)
        {
            // A malformed stored grid yields "no cell"; the caller treats that as out-of-range.
        }

        return new GridConsultationResult(null);
    }

    /// <summary>
    /// Predicate matching (US-M7-006): a recommendation is "within range" when a cell exists for the tuple AND the
    /// recommendation text matches the cell's recommended range (case-insensitive, trimmed). No cell ⇒ not within range.
    /// </summary>
    public static bool WithinRange(GridConsultationResult consultation, string recommendation)
        => consultation.RecommendedRange is { } range
           && !string.IsNullOrWhiteSpace(recommendation)
           && string.Equals(range.Trim(), recommendation.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string SnakeSeverity(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Low => "low",
        ExceptionSeverity.Medium => "medium",
        ExceptionSeverity.High => "high",
        ExceptionSeverity.Critical => "critical",
        _ => severity.ToString().ToLowerInvariant(),
    };
}
