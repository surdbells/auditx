using AuditX.Application.Ac;
using AuditX.Application.Ac.Generation;

namespace AuditX.Application.Tests.Ac;

public sealed class AcVisibilityTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AcMaterialFindingLine Finding(Guid exceptionId, string title) =>
        new(exceptionId, Guid.NewGuid(), title, "high", "open", null, Now, new DateOnly(2027, 2, 1));

    private static AcPackComposition CompositionWith(params AcMaterialFindingLine[] findings) =>
        new(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), "Q4", 1,
            TotalPlans: 1, PlanItemsTotal: 10, PlanItemsCompleted: 8, PlanCompletionPercent: 80m,
            OpenExceptionTotal: findings.Length, AverageClosureDays: 12.5,
            ExceptionsBySeverity: [new AcSeverityCountLine("high", findings.Length)],
            MaterialFindings: findings,
            SanctionsTotalCases: 0, SanctionsGridAdherencePercent: 0m, SanctionsAppealRatePercent: 0m,
            SanctionsByBusinessUnit: [],
            RecurrenceClusters: [],
            GeneratedAtUtc: Now);

    [Fact]
    public void Restricted_finding_title_is_replaced_for_a_non_allow_listed_requester()
    {
        var restrictedId = Guid.NewGuid();
        var openId = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var composition = CompositionWith(Finding(restrictedId, "Material fraud at Branch 7"), Finding(openId, "Routine gap"));

        var allowLists = new Dictionary<Guid, IReadOnlySet<Guid>>
        {
            [restrictedId] = new HashSet<Guid> { Guid.NewGuid() }, // someone else, not the requester
        };

        var redacted = AcVisibility.RedactForRequester(composition, requester, allowLists);

        var restricted = redacted.MaterialFindings.Single(f => f.ExceptionId == restrictedId);
        var open = redacted.MaterialFindings.Single(f => f.ExceptionId == openId);
        Assert.Equal(AcVisibility.RestrictedPlaceholder, restricted.Title);
        Assert.Equal("Routine gap", open.Title);
        // Aggregate counts stay coherent: the restricted row is still present.
        Assert.Equal(2, redacted.MaterialFindings.Count);
        Assert.Equal(composition.OpenExceptionTotal, redacted.OpenExceptionTotal);
    }

    [Fact]
    public void An_allow_listed_requester_sees_the_real_title()
    {
        var restrictedId = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var composition = CompositionWith(Finding(restrictedId, "Material fraud at Branch 7"));
        var allowLists = new Dictionary<Guid, IReadOnlySet<Guid>>
        {
            [restrictedId] = new HashSet<Guid> { requester },
        };

        var redacted = AcVisibility.RedactForRequester(composition, requester, allowLists);

        Assert.Equal("Material fraud at Branch 7", redacted.MaterialFindings.Single().Title);
    }

    [Fact]
    public void No_restrictions_returns_the_composition_unchanged()
    {
        var composition = CompositionWith(Finding(Guid.NewGuid(), "A"), Finding(Guid.NewGuid(), "B"));
        var redacted = AcVisibility.RedactForRequester(composition, Guid.NewGuid(), new Dictionary<Guid, IReadOnlySet<Guid>>());
        Assert.Same(composition, redacted);
    }
}
