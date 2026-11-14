using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Templates;

/// <summary>
/// An organisation-defined set of conclusion options for a verdict-based <see cref="ResponseType"/> (e.g. relabel
/// Pass/Fail/N-A to "Compliant / Partially Compliant / Non-Compliant / Not Applicable", with their own scores).
/// One set governs one response type. <see cref="OptionsJson"/> is opaque to the domain — parsed and validated in
/// the application layer (mirroring <see cref="RatingScale.PointsJson"/>) — each option carrying a stable code,
/// display label, order, an optional 0-100 score, and the semantics the engine needs (isDeficiency / isNotApplicable
/// / requiresComment). The engine derives a canonical Pass/Fail/N-A verdict from the chosen option's semantics, so
/// scoring, exception-raising and reporting keep working while the vocabulary is fully the organisation's.
/// </summary>
public sealed class ResponseOptionSet : AggregateRoot
{
    private ResponseOptionSet()
    {
    }

    /// <summary>The verdict-based response type this set governs.</summary>
    public ResponseType ResponseType { get; private set; }

    /// <summary>
    /// Null for a response type's STANDARD set — what every item of that type uses unless it names another set. A named
    /// set is an additional vocabulary (e.g. "Control effectiveness": Effective / Partially effective / Ineffective)
    /// that a checklist item can choose instead. Names are unique.
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>A retired named set is no longer offered for new items; items already using it keep working.</summary>
    public bool IsArchived { get; private set; }

    public const int MaxNameLength = 100;

    /// <summary>
    /// JSON array of options, e.g.
    /// <c>[{"code":"pass","label":"Compliant","order":0,"score":100,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false}, ...]</c>.
    /// </summary>
    public string OptionsJson { get; private set; } = null!;

    public byte[] Version { get; private set; } = [];

    public static ResponseOptionSet Create(ResponseType responseType, string optionsJson) => new()
    {
        ResponseType = responseType,
        OptionsJson = Guard.NotNullOrWhiteSpace(optionsJson, "response_option_set.options_required", "At least one option is required."),
    };

    /// <summary>Creates a named response set that checklist items of <paramref name="responseType"/> can choose.</summary>
    public static ResponseOptionSet CreateNamed(ResponseType responseType, string name, string optionsJson)
    {
        var set = Create(responseType, optionsJson);
        set.Rename(name);
        return set;
    }

    /// <summary>Renames a named set. The standard set of a response type has no name and cannot be given one.</summary>
    public void Rename(string name)
    {
        var trimmed = Guard.NotNullOrWhiteSpace(name, "response_option_set.name_required", "A response set needs a name.").Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainException("response_option_set.name_too_long", $"A response set name can be at most {MaxNameLength} characters.");
        }

        Name = trimmed;
    }

    /// <summary>Retires (or brings back) a named set. A response type's standard set is always available.</summary>
    public void SetArchived(bool archived)
    {
        if (Name is null)
        {
            throw new DomainException("response_option_set.standard_cannot_retire",
                "The standard set of a response type cannot be retired — reset it to the defaults instead.");
        }

        IsArchived = archived;
    }

    public void UpdateOptions(string optionsJson)
        => OptionsJson = Guard.NotNullOrWhiteSpace(optionsJson, "response_option_set.options_required", "At least one option is required.");
}
