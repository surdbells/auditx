namespace AuditX.Domain.Universe;

/// <summary>An active risk dimension's parameters, supplied when (re)computing composite scores.</summary>
public sealed record RiskDimensionSpec(string Name, decimal Weight, int ScaleMin, int ScaleMax);

/// <summary>
/// Computes a composite risk score as the weight-normalised average of per-dimension scores:
/// <c>composite = Σ(score_d × weight_d) / Σ(weight_d)</c> over the full active dimension set.
/// </summary>
public static class CompositeScore
{
    public static decimal? Compute(IReadOnlyDictionary<string, int> scores, IReadOnlyList<RiskDimensionSpec> activeDimensions)
    {
        if (scores.Count == 0 || activeDimensions.Count == 0)
        {
            return null;
        }

        decimal weightedSum = 0;
        decimal totalWeight = 0;
        foreach (var dimension in activeDimensions)
        {
            if (!scores.TryGetValue(dimension.Name, out var score))
            {
                continue;
            }

            weightedSum += score * dimension.Weight;
            totalWeight += dimension.Weight;
        }

        if (totalWeight == 0)
        {
            return null;
        }

        return Math.Round(weightedSum / totalWeight, 3, MidpointRounding.AwayFromZero);
    }
}
