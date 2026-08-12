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

    /// <summary>The verdict-based response type this set governs (unique across sets).</summary>
    public ResponseType ResponseType { get; private set; }

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

    public void UpdateOptions(string optionsJson)
        => OptionsJson = Guard.NotNullOrWhiteSpace(optionsJson, "response_option_set.options_required", "At least one option is required.");
}
