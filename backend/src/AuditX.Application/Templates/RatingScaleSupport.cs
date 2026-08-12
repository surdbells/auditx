using AuditX.Application.Common.Json;

namespace AuditX.Application.Templates;

/// <summary>The shape of ResponseConfigJson for a Rating-type checklist item — references a RatingScale by id.</summary>
public sealed record RatingResponseConfig(Guid? RatingScaleId);

/// <summary>The shape of ValueJson for a Rating-type response — the scale point value the auditor picked.</summary>
public sealed record RatingResponseValue(int? Rating);

/// <summary>A single point on a <see cref="Domain.Templates.RatingScale"/>: the value an auditor picks, its label, and its 0-100 score.</summary>
public sealed record RatingScalePoint(int Value, string Label, decimal Score);

/// <summary>Parsing for a <see cref="Domain.Templates.RatingScale"/>'s opaque PointsJson.</summary>
public static class RatingScalePoints
{
    public static IReadOnlyList<RatingScalePoint>? TryParse(string pointsJson)
    {
        try
        {
            return AppJson.Deserialize<List<RatingScalePoint>>(pointsJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
