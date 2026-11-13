using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Templates;

/// <summary>
/// The per-type settings a checklist item carries in its opaque <c>ResponseConfigJson</c>: the rating scale a
/// <see cref="ResponseType.Rating"/> item is answered on, the choices of a <see cref="ResponseType.MultipleChoice"/>
/// item, and the unit of a <see cref="ResponseType.Numeric"/> item. <see cref="Max"/> is the legacy "1..max" rating
/// shape, read (never written) so an item saved before rating scales existed still renders.
/// </summary>
public sealed record ResponseItemConfig(
    Guid? RatingScaleId = null, IReadOnlyList<string>? Options = null, string? Unit = null, int? Max = null)
{
    public static readonly ResponseItemConfig Empty = new();

    /// <summary>Lenient read: malformed or absent JSON yields <see cref="Empty"/> rather than throwing.</summary>
    public static ResponseItemConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            return AppJson.Deserialize<ResponseItemConfig>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return Empty;
        }
    }
}

/// <summary>
/// The write-path rules for a checklist item's response settings, shared by template items and audit checklist
/// items so the two cannot drift: every <see cref="ResponseType"/> must be saved in a state an auditor can actually
/// answer. Returns the CANONICAL settings JSON (only the keys the type uses), so stale settings left over from a
/// previous type never linger on the item.
/// </summary>
public static class ResponseItemConfigRules
{
    public const int MinChoices = 2;
    public const int MaxChoices = 50;
    public const int MaxChoiceLength = 200;
    public const int MaxUnitLength = 30;

    public static async Task<string?> NormaliseAsync(
        ResponseType responseType, string? responseConfigJson, IRatingScaleRepository ratingScales, CancellationToken cancellationToken)
    {
        var config = ResponseItemConfig.Parse(responseConfigJson);
        switch (responseType)
        {
            case ResponseType.Rating:
            {
                // A rating is answered (and scored) on a configured scale — without one there is nothing to pick from.
                var scaleId = config.RatingScaleId
                    ?? throw new ConflictException("template.rating_scale_required", "A rating item must reference a rating scale.");
                if (await ratingScales.GetByIdAsync(scaleId, cancellationToken) is null)
                {
                    throw new NotFoundException("Rating scale", scaleId);
                }

                return AppJson.Serialize(new { ratingScaleId = scaleId });
            }

            case ResponseType.MultipleChoice:
                return AppJson.Serialize(new { options = NormaliseChoices(config.Options) });

            case ResponseType.Numeric:
            {
                var unit = string.IsNullOrWhiteSpace(config.Unit) ? null : config.Unit.Trim();
                if (unit is { Length: > MaxUnitLength })
                {
                    throw new DomainException("response_config.unit_too_long", $"A unit can be at most {MaxUnitLength} characters.");
                }

                return unit is null ? null : AppJson.Serialize(new { unit });
            }

            default:
                // Pass/Fail/N-A, Yes/No, Text and Date carry no per-item settings.
                return null;
        }
    }

    private static string[] NormaliseChoices(IReadOnlyList<string>? options)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var choices = new List<string>();
        foreach (var raw in options ?? [])
        {
            var choice = raw?.Trim();
            if (string.IsNullOrEmpty(choice))
            {
                continue;
            }

            if (choice.Length > MaxChoiceLength)
            {
                throw new DomainException("response_config.choice_too_long", $"A choice can be at most {MaxChoiceLength} characters.");
            }

            if (!seen.Add(choice))
            {
                throw new DomainException("response_config.duplicate_choice", $"The choice '{choice}' is listed more than once.");
            }

            choices.Add(choice);
        }

        if (choices.Count < MinChoices)
        {
            throw new DomainException("response_config.choices_required", $"A multiple-choice item needs at least {MinChoices} choices.");
        }

        if (choices.Count > MaxChoices)
        {
            throw new DomainException("response_config.too_many_choices", $"A multiple-choice item can have at most {MaxChoices} choices.");
        }

        return [.. choices];
    }
}
