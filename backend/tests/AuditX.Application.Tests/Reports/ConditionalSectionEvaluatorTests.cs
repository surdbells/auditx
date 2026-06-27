using AuditX.Application.Reports.Generation;

namespace AuditX.Application.Tests.Reports;

public sealed class ConditionalSectionEvaluatorTests
{
    [Fact]
    public void Unconditional_section_always_renders()
        => Assert.True(ConditionalSectionEvaluator.ShouldRender(null, new Dictionary<string, bool>()));

    [Fact]
    public void Known_flag_renders_per_its_value()
    {
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [ConditionalSectionEvaluator.HasEvidenceFiles] = true,
            [ConditionalSectionEvaluator.HasCriticalExceptions] = false,
        };
        Assert.True(ConditionalSectionEvaluator.ShouldRender(ConditionalSectionEvaluator.HasEvidenceFiles, flags));
        Assert.False(ConditionalSectionEvaluator.ShouldRender(ConditionalSectionEvaluator.HasCriticalExceptions, flags));
    }

    [Fact]
    public void Unknown_flag_never_renders()
        => Assert.False(ConditionalSectionEvaluator.ShouldRender("arbitrary_unknown_flag", new Dictionary<string, bool>()));
}
