using AuditX.Domain.Common;
using AuditX.Domain.Universe;

namespace AuditX.Domain.Tests.Universe;

public sealed class RiskScoringTests
{
    private static readonly IReadOnlyList<RiskDimensionSpec> Dimensions =
    [
        new("Financial", 1m, 1, 5),
        new("Operational", 1m, 1, 5),
        new("Regulatory", 2m, 1, 5),
    ];

    [Fact]
    public void Composite_is_weight_normalised_average()
    {
        var scores = new Dictionary<string, int> { ["Financial"] = 4, ["Operational"] = 2, ["Regulatory"] = 5 };
        // (4*1 + 2*1 + 5*2) / (1+1+2) = 16/4 = 4.000
        Assert.Equal(4.000m, CompositeScore.Compute(scores, Dimensions));
    }

    [Fact]
    public void ApplyRiskScores_sets_composites_and_raises_event()
    {
        var entity = AuditableEntity.Create("branch", "Lagos HQ", null, null, null);
        var scores = new Dictionary<string, int> { ["Financial"] = 3, ["Operational"] = 3, ["Regulatory"] = 3 };

        entity.ApplyRiskScores(scores, scores, Dimensions);

        Assert.Equal(3.000m, entity.CompositeInherentScore);
        Assert.Equal(3.000m, entity.CompositeResidualScore);
        Assert.Contains(entity.DomainEvents, e => e is Domain.Universe.Events.EntityRiskScoredEvent);
    }

    [Fact]
    public void Incomplete_score_set_is_rejected()
    {
        var entity = AuditableEntity.Create("branch", "X", null, null, null);
        var partial = new Dictionary<string, int> { ["Financial"] = 3, ["Operational"] = 3 }; // missing Regulatory

        var ex = Assert.Throws<DomainException>(() => entity.ApplyRiskScores(partial, null, Dimensions));
        Assert.Equal("universe.incomplete_scores", ex.Code);
    }

    [Fact]
    public void Unknown_dimension_is_rejected()
    {
        var entity = AuditableEntity.Create("branch", "X", null, null, null);
        var scores = new Dictionary<string, int> { ["Financial"] = 3, ["Operational"] = 3, ["Regulatory"] = 3, ["Cyber"] = 4 };

        Assert.Throws<DomainException>(() => entity.ApplyRiskScores(scores, null, Dimensions));
    }

    [Fact]
    public void Out_of_range_score_is_rejected()
    {
        var entity = AuditableEntity.Create("branch", "X", null, null, null);
        var scores = new Dictionary<string, int> { ["Financial"] = 9, ["Operational"] = 3, ["Regulatory"] = 3 };

        var ex = Assert.Throws<DomainException>(() => entity.ApplyRiskScores(scores, null, Dimensions));
        Assert.Equal("universe.score_out_of_range", ex.Code);
    }

    [Fact]
    public void Entity_cannot_be_its_own_parent()
    {
        var entity = AuditableEntity.Create("branch", "X", null, null, null);
        Assert.Throws<DomainException>(() => entity.SetParent(entity.Id));
    }
}
