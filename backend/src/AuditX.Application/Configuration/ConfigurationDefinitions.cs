using System.Text.Json;
using AuditX.Domain.Common;
using AuditX.Domain.Configuration;

namespace AuditX.Application.Configuration;

/// <summary>
/// Per-domain serialization + schema validation for configuration definitions (M12, G1). JSON keys are snake_case to
/// match the platform convention; the typed shape is <see cref="ExceptionDefaultsDefinition"/>. Validation is run on
/// CREATE and re-run on ACTIVATE; a bad definition throws <see cref="DomainException"/> → HTTP 422.
/// </summary>
public static class ConfigurationDefinitions
{
    private static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Validate a domain's definition JSON, throwing <see cref="DomainException"/> (422) on any violation.</summary>
    public static void Validate(string domain, string definitionJson)
    {
        switch (domain)
        {
            case ConfigurationDomains.ExceptionDefaults:
                _ = ParseExceptionDefaults(definitionJson);
                break;
            default:
                throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{domain}'.");
        }
    }

    /// <summary>Serialize a typed <see cref="ExceptionDefaultsDefinition"/> to its canonical snake_case JSON.</summary>
    public static string SerializeExceptionDefaults(ExceptionDefaultsDefinition definition)
        => JsonSerializer.Serialize(new ExceptionDefaultsDocument(
            new TargetDaysDocument(definition.CriticalTargetDays, definition.HighTargetDays, definition.MediumTargetDays, definition.LowTargetDays),
            definition.RecurrenceWindowMonths,
            definition.RecurrenceThreshold));

    /// <summary>
    /// Parse + validate an <c>exception_defaults</c> definition JSON into the typed shape. Throws
    /// <see cref="DomainException"/> (422) when the JSON is malformed, missing required fields, or out of range
    /// (target_days positive ints; recurrence_window_months 1–120; recurrence_threshold ≥ 2).
    /// </summary>
    public static ExceptionDefaultsDefinition ParseExceptionDefaults(string definitionJson)
    {
        ExceptionDefaultsDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<ExceptionDefaultsDocument>(definitionJson, ParseOptions);
        }
        catch (JsonException)
        {
            throw new DomainException("configuration.invalid_definition", "The configuration definition is not valid JSON.");
        }

        if (doc?.TargetDays is not { } days)
        {
            throw new DomainException("configuration.invalid_definition", "target_days is required (critical, high, medium, low).");
        }

        RequirePositive(days.Critical, "target_days.critical");
        RequirePositive(days.High, "target_days.high");
        RequirePositive(days.Medium, "target_days.medium");
        RequirePositive(days.Low, "target_days.low");

        if (doc.RecurrenceWindowMonths is < 1 or > 120)
        {
            throw new DomainException("configuration.invalid_definition", "recurrence_window_months must be between 1 and 120.");
        }

        if (doc.RecurrenceThreshold < 2)
        {
            throw new DomainException("configuration.invalid_definition", "recurrence_threshold must be at least 2.");
        }

        return new ExceptionDefaultsDefinition(
            days.Critical, days.High, days.Medium, days.Low, doc.RecurrenceWindowMonths, doc.RecurrenceThreshold);
    }

    private static void RequirePositive(int value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException("configuration.invalid_definition", $"{field} must be a positive integer.");
        }
    }

    // snake_case mapped via the JsonPropertyName attributes below; serialized canonically with these names.
    private sealed record ExceptionDefaultsDocument(
        [property: System.Text.Json.Serialization.JsonPropertyName("target_days")] TargetDaysDocument? TargetDays,
        [property: System.Text.Json.Serialization.JsonPropertyName("recurrence_window_months")] int RecurrenceWindowMonths,
        [property: System.Text.Json.Serialization.JsonPropertyName("recurrence_threshold")] int RecurrenceThreshold);

    private sealed record TargetDaysDocument(
        [property: System.Text.Json.Serialization.JsonPropertyName("critical")] int Critical,
        [property: System.Text.Json.Serialization.JsonPropertyName("high")] int High,
        [property: System.Text.Json.Serialization.JsonPropertyName("medium")] int Medium,
        [property: System.Text.Json.Serialization.JsonPropertyName("low")] int Low);
}
