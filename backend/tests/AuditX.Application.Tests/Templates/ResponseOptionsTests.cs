using AuditX.Application.Templates;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Tests.Templates;

public sealed class ResponseOptionsTests
{
    [Fact]
    public void Defaults_reproduce_pass_fail_na_semantics()
    {
        var options = ResponseOptions.Defaults(ResponseType.PassFailNa);

        var pass = options.Single(o => o.Code == "pass");
        var fail = options.Single(o => o.Code == "fail");
        var na = options.Single(o => o.Code == "na");

        Assert.Equal(ResponseVerdict.Pass, pass.CanonicalVerdict);
        Assert.Equal(100m, pass.Score);
        Assert.Equal(ResponseVerdict.Fail, fail.CanonicalVerdict);
        Assert.True(fail.IsDeficiency);
        Assert.Equal(ResponseVerdict.Na, na.CanonicalVerdict);
        Assert.True(na.IsNotApplicable);
    }

    [Fact]
    public void Canonical_verdict_is_derived_from_option_semantics()
    {
        // A custom "Partially Compliant" option that is a finding with a mid score maps to Fail for the engine.
        var partial = new ResponseOption("partial", "Partially Compliant", 1, 50m, IsDeficiency: true, IsNotApplicable: false, RequiresComment: true);
        Assert.Equal(ResponseVerdict.Fail, partial.CanonicalVerdict);

        // A non-finding "Largely Compliant" maps to Pass.
        var largely = new ResponseOption("largely", "Largely Compliant", 0, 80m, IsDeficiency: false, IsNotApplicable: false, RequiresComment: false);
        Assert.Equal(ResponseVerdict.Pass, largely.CanonicalVerdict);
    }

    [Fact]
    public void ParseOrThrow_accepts_a_valid_custom_set_and_orders_by_order()
    {
        const string json = """
            [
              {"code":"nc","label":"Non-Compliant","order":3,"score":0,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true},
              {"code":"c","label":"Compliant","order":0,"score":100,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false},
              {"code":"pc","label":"Partially Compliant","order":1,"score":50,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true},
              {"code":"na","label":"Not Applicable","order":4,"score":null,"isDeficiency":false,"isNotApplicable":true,"requiresComment":false}
            ]
            """;
        var parsed = ResponseOptions.ParseOrThrow(json);
        Assert.Equal(new[] { "c", "pc", "nc", "na" }, parsed.Select(o => o.Code).ToArray());
    }

    [Fact]
    public void ParseOrThrow_rejects_duplicate_codes()
    {
        const string json = """
            [
              {"code":"c","label":"Compliant","order":0,"score":100,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false},
              {"code":"c","label":"Compliant Again","order":1,"score":50,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false}
            ]
            """;
        var ex = Assert.Throws<DomainException>(() => ResponseOptions.ParseOrThrow(json));
        Assert.Equal("response_option_set.duplicate_code", ex.Code);
    }

    [Fact]
    public void ParseOrThrow_rejects_a_score_out_of_range()
    {
        const string json = """[{"code":"x","label":"X","order":0,"score":150,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false}]""";
        var ex = Assert.Throws<DomainException>(() => ResponseOptions.ParseOrThrow(json));
        Assert.Equal("response_option_set.score_range", ex.Code);
    }

    [Fact]
    public void ParseOrThrow_rejects_an_all_na_set()
    {
        const string json = """[{"code":"na","label":"N/A","order":0,"score":null,"isDeficiency":false,"isNotApplicable":true,"requiresComment":false}]""";
        var ex = Assert.Throws<DomainException>(() => ResponseOptions.ParseOrThrow(json));
        Assert.Equal("response_option_set.needs_conclusive", ex.Code);
    }

    [Fact]
    public void Only_verdict_types_support_option_sets()
    {
        Assert.True(ResponseOptions.SupportsOptionSet(ResponseType.PassFailNa));
        Assert.True(ResponseOptions.SupportsOptionSet(ResponseType.YesNo));
        Assert.False(ResponseOptions.SupportsOptionSet(ResponseType.Rating));
        Assert.False(ResponseOptions.SupportsOptionSet(ResponseType.Text));
    }
}
