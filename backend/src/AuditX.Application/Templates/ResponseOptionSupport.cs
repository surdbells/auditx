using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Json;
using AuditX.Domain.Enums;

namespace AuditX.Application.Templates;

/// <summary>
/// One organisation-defined conclusion option. <see cref="Code"/> is a stable identifier stored on the response;
/// <see cref="Label"/> is what the auditor sees. The engine reads the semantics: <see cref="Score"/> feeds
/// post-response scoring, <see cref="IsDeficiency"/> marks the option as a finding (drives exception-raising),
/// <see cref="IsNotApplicable"/> excludes it from scoring, and <see cref="RequiresComment"/> forces a comment.
/// </summary>
public sealed record ResponseOption(
    string Code, string Label, int Order, decimal? Score, bool IsDeficiency, bool IsNotApplicable, bool RequiresComment)
{
    /// <summary>The canonical Pass/Fail/N-A verdict this option maps to, so the exception/analytics engine stays stable.</summary>
    public ResponseVerdict CanonicalVerdict =>
        IsNotApplicable ? ResponseVerdict.Na : IsDeficiency ? ResponseVerdict.Fail : ResponseVerdict.Pass;
}

/// <summary>Parsing + validation for a <see cref="Domain.Templates.ResponseOptionSet"/>'s opaque OptionsJson.</summary>
public static class ResponseOptions
{
    /// <summary>Only verdict-based response types have configurable conclusion options.</summary>
    public static bool SupportsOptionSet(ResponseType type) => type is ResponseType.PassFailNa or ResponseType.YesNo;

    public static IReadOnlyList<ResponseOption>? TryParse(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return null;
        }

        try
        {
            var options = AppJson.Deserialize<List<ResponseOption>>(optionsJson);
            return options is null || options.Count == 0 ? null : options.OrderBy(o => o.Order).ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Validate a proposed option list (used by the admin write path); throws a friendly domain error.</summary>
    public static IReadOnlyList<ResponseOption> ParseOrThrow(string? optionsJson)
    {
        var options = TryParse(optionsJson)
            ?? throw new Domain.Common.DomainException("response_option_set.invalid", "At least one valid option is required.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in options)
        {
            if (string.IsNullOrWhiteSpace(o.Code) || string.IsNullOrWhiteSpace(o.Label))
            {
                throw new Domain.Common.DomainException("response_option_set.option_incomplete", "Every option needs a code and a label.");
            }

            if (!seen.Add(o.Code))
            {
                throw new Domain.Common.DomainException("response_option_set.duplicate_code", $"Duplicate option code '{o.Code}'.");
            }

            if (o.Score is { } s && (s < 0 || s > 100))
            {
                throw new Domain.Common.DomainException("response_option_set.score_range", "An option score must be between 0 and 100.");
            }
        }

        if (options.All(o => o.IsNotApplicable))
        {
            throw new Domain.Common.DomainException("response_option_set.needs_conclusive", "At least one option must be a real conclusion (not all Not-Applicable).");
        }

        return options;
    }

    /// <summary>The built-in defaults that reproduce today's fixed verdicts, per verdict-based response type.</summary>
    public static IReadOnlyList<ResponseOption> Defaults(ResponseType type) => type switch
    {
        ResponseType.YesNo =>
        [
            new("pass", "Yes", 0, 100m, false, false, false),
            new("fail", "No", 1, 0m, true, false, true),
            new("na", "N/A", 2, null, false, true, true),
        ],
        _ =>
        [
            new("pass", "Pass", 0, 100m, false, false, false),
            new("fail", "Fail", 1, 0m, true, false, true),
            new("na", "N/A", 2, null, false, true, true),
        ],
    };

    public static string DefaultsJson(ResponseType type) => AppJson.Serialize(Defaults(type));
}
