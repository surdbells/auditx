using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Json;
using AuditX.Application.Templates;
using AuditX.Domain.Enums;

namespace AuditX.Application.Execution.Services;

public interface IResponseScoringService
{
    /// <summary>
    /// The 0-100 score a finalised response earns, or null if a draft or if this response type/verdict has no
    /// score mapping. See <see cref="ResponseScoringService"/> for the mapping rules.
    /// </summary>
    Task<decimal?> ComputeScoreAsync(
        ResponseType responseType, string? responseConfigJson, ResponseVerdict? verdict, string? valueJson, bool isDraft,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Post-response scoring (M5): a finalised response earns a 0-100 score once submitted. Pass/Fail verdict
/// types score via a fixed Pass=100/Fail=0 mapping (N/A is excluded — it is neither compliant nor a finding).
/// A Rating value scores via the point the auditor picked on its configured <see cref="Domain.Templates.RatingScale"/>.
/// Value types with no inherent right answer (text/numeric/date/multiple-choice) are never scored — there is
/// nothing on the item to derive a score from.
/// </summary>
public sealed class ResponseScoringService(IRatingScaleRepository ratingScales) : IResponseScoringService
{
    public async Task<decimal?> ComputeScoreAsync(
        ResponseType responseType, string? responseConfigJson, ResponseVerdict? verdict, string? valueJson, bool isDraft,
        CancellationToken cancellationToken = default)
    {
        if (isDraft)
        {
            return null;
        }

        if (!responseType.IsValueType())
        {
            return verdict switch
            {
                ResponseVerdict.Pass => 100m,
                ResponseVerdict.Fail => 0m,
                _ => null,
            };
        }

        if (responseType != ResponseType.Rating)
        {
            return null;
        }

        var scaleId = ParseRatingScaleId(responseConfigJson);
        var ratingValue = ParseRatingValue(valueJson);
        if (scaleId is null || ratingValue is null)
        {
            return null;
        }

        var scale = await ratingScales.GetByIdAsync(scaleId.Value, cancellationToken);
        if (scale is null)
        {
            return null;
        }

        var points = RatingScalePoints.TryParse(scale.PointsJson) ?? [];
        return points.FirstOrDefault(p => p.Value == ratingValue.Value)?.Score;
    }

    private static Guid? ParseRatingScaleId(string? responseConfigJson)
    {
        if (string.IsNullOrWhiteSpace(responseConfigJson))
        {
            return null;
        }

        try
        {
            return AppJson.Deserialize<RatingResponseConfig>(responseConfigJson)?.RatingScaleId;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static int? ParseRatingValue(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
        {
            return null;
        }

        try
        {
            return AppJson.Deserialize<RatingResponseValue>(valueJson)?.Rating;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
