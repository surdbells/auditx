using System.Globalization;
using System.Text.Json;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Json;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Templates;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Execution.Services;

public interface IResponseInputResolver
{
    /// <summary>
    /// What the responder is asked for on an item of this type: the organisation's conclusion options, the points of
    /// the configured rating scale, the item's choices, or its unit. Resolved server-side so answering an item never
    /// depends on the responder being allowed to browse the template library.
    /// </summary>
    Task<ResponseInputDto> ResolveAsync(ResponseType responseType, string? responseConfigJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a submitted value against the item's type and settings and return it in canonical form (or null when
    /// nothing was captured). Throws <see cref="DomainException"/> (<c>response.invalid_value</c>) when the value is
    /// not one the item can accept — a rating off the scale, a choice that is not offered, a malformed date or number.
    /// </summary>
    Task<string?> NormaliseValueAsync(ResponseType responseType, string? responseConfigJson, string? valueJson, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IResponseInputResolver"/>
public sealed class ResponseInputResolver(IResponseOptionSetRepository optionSets, IRatingScaleRepository ratingScales)
    : IResponseInputResolver
{
    /// <summary>Upper bound on a free-text response.</summary>
    public const int MaxTextLength = 4000;

    /// <summary>The "1..max" fallback for a legacy rating item saved before rating scales existed.</summary>
    private const int LegacyDefaultMax = 5;

    public async Task<ResponseInputDto> ResolveAsync(ResponseType responseType, string? responseConfigJson, CancellationToken cancellationToken = default)
    {
        var config = ResponseItemConfig.Parse(responseConfigJson);
        var type = responseType.ToSnake();
        switch (responseType)
        {
            case ResponseType.PassFailNa:
            case ResponseType.YesNo:
            {
                var set = await optionSets.GetByResponseTypeAsync(responseType, cancellationToken);
                var options = (set is null ? null : ResponseOptions.TryParse(set.OptionsJson)) ?? ResponseOptions.Defaults(responseType);
                return new ResponseInputDto(type,
                    Options: options.Select(o => new ResponseInputOptionDto(o.Code, o.Label, o.IsDeficiency, o.IsNotApplicable, o.RequiresComment)).ToArray());
            }

            case ResponseType.Rating:
                return new ResponseInputDto(type, RatingPoints: await ResolveRatingPointsAsync(config, cancellationToken));

            case ResponseType.MultipleChoice:
                return new ResponseInputDto(type, Choices: config.Options?.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray() ?? []);

            case ResponseType.Numeric:
                return new ResponseInputDto(type, Unit: string.IsNullOrWhiteSpace(config.Unit) ? null : config.Unit.Trim());

            default:
                return new ResponseInputDto(type);
        }
    }

    public async Task<string?> NormaliseValueAsync(ResponseType responseType, string? responseConfigJson, string? valueJson, CancellationToken cancellationToken = default)
    {
        // Verdict types capture a conclusion, not a value — anything sent alongside is dropped rather than stored.
        if (!responseType.IsValueType() || string.IsNullOrWhiteSpace(valueJson))
        {
            return null;
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Invalid("The response value is not valid JSON.");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("The response value must be an object.");
        }

        var config = ResponseItemConfig.Parse(responseConfigJson);
        switch (responseType)
        {
            case ResponseType.Text:
            {
                var text = ReadString(root, "text");
                if (text is null)
                {
                    return null;
                }

                if (text.Length > MaxTextLength)
                {
                    throw Invalid($"A text response can be at most {MaxTextLength} characters.");
                }

                return AppJson.Serialize(new { text });
            }

            case ResponseType.Numeric:
            {
                if (!root.TryGetProperty("number", out var element) || element.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var number))
                {
                    throw Invalid("A numeric response must be a number.");
                }

                return AppJson.Serialize(new { number });
            }

            case ResponseType.Date:
            {
                var raw = ReadString(root, "date");
                if (raw is null)
                {
                    return null;
                }

                if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    throw Invalid("A date response must be a calendar date (yyyy-MM-dd).");
                }

                return AppJson.Serialize(new { date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
            }

            case ResponseType.Rating:
            {
                if (!root.TryGetProperty("rating", out var element) || element.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var rating))
                {
                    throw Invalid("A rating must be a whole number.");
                }

                var points = await ResolveRatingPointsAsync(config, cancellationToken);
                var point = points.FirstOrDefault(p => p.Value == rating)
                    ?? throw Invalid($"{rating} is not a point on this item's rating scale.");

                // Snapshot the point's label with the value, so the response keeps reading as the auditor saw it
                // ("4 – Effective") even if the scale is relabelled later. Null (omitted) for an unlabelled point.
                var label = string.IsNullOrWhiteSpace(point.Label) ? null : point.Label.Trim();
                return AppJson.Serialize(new { rating, label });
            }

            case ResponseType.MultipleChoice:
            {
                var raw = ReadString(root, "choice");
                if (raw is null)
                {
                    return null;
                }

                // Store the choice exactly as the item defines it, whatever casing the client sent.
                var choice = config.Options?.FirstOrDefault(o => string.Equals(o?.Trim(), raw, StringComparison.OrdinalIgnoreCase))?.Trim()
                    ?? throw Invalid($"'{raw}' is not one of this item's choices.");
                return AppJson.Serialize(new { choice });
            }

            default:
                return null;
        }
    }

    private async Task<IReadOnlyList<ResponseInputRatingPointDto>> ResolveRatingPointsAsync(ResponseItemConfig config, CancellationToken cancellationToken)
    {
        if (config.RatingScaleId is { } scaleId && await ratingScales.GetByIdAsync(scaleId, cancellationToken) is { } scale)
        {
            var points = RatingScalePoints.TryParse(scale.PointsJson);
            if (points is { Count: > 0 })
            {
                return points.OrderBy(p => p.Value).Select(p => new ResponseInputRatingPointDto(p.Value, p.Label)).ToArray();
            }
        }

        // Legacy item with no scale: a bare 1..max so it stays answerable (it carries no score mapping).
        var max = Math.Clamp(config.Max ?? LegacyDefaultMax, 2, 10);
        return Enumerable.Range(1, max).Select(v => new ResponseInputRatingPointDto(v, string.Empty)).ToArray();
    }

    private static string? ReadString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"The response '{property}' must be text.");
        }

        var value = element.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static DomainException Invalid(string message) => new("response.invalid_value", message);
}
