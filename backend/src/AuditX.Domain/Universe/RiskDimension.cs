using AuditX.Domain.Common;

namespace AuditX.Domain.Universe;

/// <summary>
/// A configurable risk-scoring dimension (M3). Weights are relative — composite scores normalise by the
/// sum of active weights, so weights need not sum to 1. Scores are integers within [ScaleMin, ScaleMax].
/// </summary>
public sealed class RiskDimension : AggregateRoot
{
    private RiskDimension()
    {
    }

    public string Name { get; private set; } = null!;

    public decimal Weight { get; private set; }

    public int ScaleMin { get; private set; } = 1;

    public int ScaleMax { get; private set; } = 5;

    public string? ScaleLabelOverridesJson { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static RiskDimension Create(string name, decimal weight, int scaleMin, int scaleMax, string? scaleLabelOverridesJson)
    {
        var dimension = new RiskDimension
        {
            Name = Guard.NotNullOrWhiteSpace(name, "dimension.name_required", "Dimension name is required."),
            ScaleLabelOverridesJson = scaleLabelOverridesJson,
            IsActive = true,
        };
        dimension.SetWeight(weight);
        dimension.SetScale(scaleMin, scaleMax);
        return dimension;
    }

    public void Update(decimal? weight, int? scaleMin, int? scaleMax, bool? isActive, string? scaleLabelOverridesJson)
    {
        if (weight is { } w)
        {
            SetWeight(w);
        }

        if (scaleMin is { } min && scaleMax is { } max)
        {
            SetScale(min, max);
        }
        else if (scaleMin is { } onlyMin)
        {
            SetScale(onlyMin, ScaleMax);
        }
        else if (scaleMax is { } onlyMax)
        {
            SetScale(ScaleMin, onlyMax);
        }

        if (isActive is { } active)
        {
            IsActive = active;
        }

        if (scaleLabelOverridesJson is not null)
        {
            ScaleLabelOverridesJson = scaleLabelOverridesJson;
        }
    }

    public RiskDimensionSpec ToSpec() => new(Name, Weight, ScaleMin, ScaleMax);

    private void SetWeight(decimal weight)
    {
        if (weight <= 0)
        {
            throw new DomainException("dimension.weight_invalid", "Weight must be greater than zero.");
        }

        Weight = weight;
    }

    private void SetScale(int scaleMin, int scaleMax)
    {
        if (scaleMax <= scaleMin)
        {
            throw new DomainException("dimension.scale_invalid", "ScaleMax must be greater than ScaleMin.");
        }

        ScaleMin = scaleMin;
        ScaleMax = scaleMax;
    }
}

/// <summary>The bank-configurable taxonomy of auditable entity types (e.g. branch, process, system).</summary>
public sealed class EntityTypeTaxonomy : Entity
{
    private EntityTypeTaxonomy()
    {
    }

    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    public static EntityTypeTaxonomy Create(string name) => new()
    {
        Name = Guard.NotNullOrWhiteSpace(name, "taxonomy.name_required", "Entity type name is required.").Trim(),
        IsActive = true,
    };
}
