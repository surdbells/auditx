using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Execution.Services;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;
using NSubstitute;

namespace AuditX.Application.Tests.Templates;

/// <summary>
/// The respond form is told what an item offers by the server, and a submitted value must be one the item can
/// accept — for every response type.
/// </summary>
public sealed class ResponseInputResolverTests
{
    private static readonly RatingScale Scale = RatingScale.Create(
        "Effectiveness", null,
        """[{"value":3,"label":"Strong","score":100},{"value":1,"label":"Weak","score":0},{"value":2,"label":"","score":50}]""");

    private static string RatingConfig => $$"""{"ratingScaleId":"{{Scale.Id}}"}""";

    private const string ChoiceConfig = """{"options":["Fully compliant","Partially compliant","Not compliant"]}""";

    private static ResponseInputResolver Resolver(ResponseOptionSet? optionSet = null)
    {
        var sets = Substitute.For<IResponseOptionSetRepository>();
        sets.GetByResponseTypeAsync(Arg.Any<ResponseType>(), Arg.Any<CancellationToken>()).Returns(optionSet);
        var scales = Substitute.For<IRatingScaleRepository>();
        scales.GetByIdAsync(Scale.Id, Arg.Any<CancellationToken>()).Returns(Scale);
        return new ResponseInputResolver(sets, scales);
    }

    // ---- What the form is offered ------------------------------------------------------------

    [Fact]
    public async Task Verdict_types_offer_the_built_in_conclusions_when_nothing_is_customised()
    {
        var passFail = await Resolver().ResolveAsync(ResponseType.PassFailNa, null);
        var yesNo = await Resolver().ResolveAsync(ResponseType.YesNo, null);

        Assert.Equal(["Pass", "Fail", "N/A"], passFail.Options!.Select(o => o.Label));
        Assert.Equal(["Yes", "No", "N/A"], yesNo.Options!.Select(o => o.Label));
        Assert.Equal("yes_no", yesNo.ResponseType);
    }

    [Fact]
    public async Task Verdict_types_offer_the_organisations_own_labels_when_customised()
    {
        var custom = ResponseOptionSet.Create(ResponseType.PassFailNa,
            """[{"code":"ok","label":"Compliant","order":0,"score":100,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false},{"code":"bad","label":"Non-compliant","order":1,"score":0,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true}]""");

        var input = await Resolver(custom).ResolveAsync(ResponseType.PassFailNa, null);

        Assert.Equal(["Compliant", "Non-compliant"], input.Options!.Select(o => o.Label));
        Assert.True(input.Options![1].IsDeficiency);
        Assert.True(input.Options![1].RequiresComment);
    }

    [Fact]
    public async Task Rating_offers_the_scale_points_in_value_order()
    {
        var input = await Resolver().ResolveAsync(ResponseType.Rating, RatingConfig);

        Assert.Equal([1, 2, 3], input.RatingPoints!.Select(p => p.Value));
        Assert.Equal("Weak", input.RatingPoints![0].Label);
    }

    [Fact]
    public async Task A_legacy_rating_with_no_scale_stays_answerable_on_a_bare_range()
    {
        var input = await Resolver().ResolveAsync(ResponseType.Rating, """{"max":4}""");

        Assert.Equal([1, 2, 3, 4], input.RatingPoints!.Select(p => p.Value));
    }

    [Fact]
    public async Task Multiple_choice_and_numeric_expose_their_settings()
    {
        var choice = await Resolver().ResolveAsync(ResponseType.MultipleChoice, ChoiceConfig);
        var numeric = await Resolver().ResolveAsync(ResponseType.Numeric, """{"unit":"days"}""");

        Assert.Equal(3, choice.Choices!.Count);
        Assert.Equal("days", numeric.Unit);
    }

    // ---- What the server accepts ---------------------------------------------------------------

    [Theory]
    [InlineData(ResponseType.PassFailNa)]
    [InlineData(ResponseType.YesNo)]
    public async Task A_verdict_item_never_keeps_a_stray_value(ResponseType type)
    {
        Assert.Null(await Resolver().NormaliseValueAsync(type, null, """{"text":"should not be stored"}"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("""{"text":"   "}""")]
    [InlineData("""{}""")]
    public async Task Nothing_captured_normalises_to_null_so_the_domain_can_require_it(string? valueJson)
    {
        Assert.Null(await Resolver().NormaliseValueAsync(ResponseType.Text, null, valueJson));
    }

    [Fact]
    public async Task Text_is_trimmed_and_stored_canonically()
    {
        var result = await Resolver().NormaliseValueAsync(ResponseType.Text, null, """{"text":"  Reviewed.  ","junk":1}""");

        Assert.Equal("""{"text":"Reviewed."}""", result);
    }

    [Fact]
    public async Task Text_beyond_the_limit_is_rejected()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { text = new string('x', ResponseInputResolver.MaxTextLength + 1) });

        await AssertInvalid(() => Resolver().NormaliseValueAsync(ResponseType.Text, null, json));
    }

    [Fact]
    public async Task Numeric_accepts_a_number_and_rejects_anything_else()
    {
        Assert.Equal("""{"number":12.5}""", await Resolver().NormaliseValueAsync(ResponseType.Numeric, null, """{"number":12.5}"""));
        Assert.Equal("""{"number":-3}""", await Resolver().NormaliseValueAsync(ResponseType.Numeric, null, """{"number":-3}"""));

        await AssertInvalid(() => Resolver().NormaliseValueAsync(ResponseType.Numeric, null, """{"number":"twelve"}"""));
        await AssertInvalid(() => Resolver().NormaliseValueAsync(ResponseType.Numeric, null, """{"number":true}"""));
    }

    [Fact]
    public async Task Date_accepts_an_iso_calendar_date_only()
    {
        Assert.Equal("""{"date":"2026-11-13"}""", await Resolver().NormaliseValueAsync(ResponseType.Date, null, """{"date":"2026-11-13"}"""));

        await AssertInvalid(() => Resolver().NormaliseValueAsync(ResponseType.Date, null, """{"date":"13/11/2026"}"""));
        await AssertInvalid(() => Resolver().NormaliseValueAsync(ResponseType.Date, null, """{"date":"2026-02-30"}"""));
    }

    [Fact]
    public async Task Rating_must_be_a_point_on_the_items_scale_and_snapshots_its_label()
    {
        var resolver = Resolver();

        Assert.Equal("""{"rating":3,"label":"Strong"}""", await resolver.NormaliseValueAsync(ResponseType.Rating, RatingConfig, """{"rating":3}"""));
        // An unlabelled point stores no label.
        Assert.Equal("""{"rating":2}""", await resolver.NormaliseValueAsync(ResponseType.Rating, RatingConfig, """{"rating":2}"""));

        await AssertInvalid(() => resolver.NormaliseValueAsync(ResponseType.Rating, RatingConfig, """{"rating":4}"""));
        await AssertInvalid(() => resolver.NormaliseValueAsync(ResponseType.Rating, RatingConfig, """{"rating":2.5}"""));
        await AssertInvalid(() => resolver.NormaliseValueAsync(ResponseType.Rating, RatingConfig, """{"rating":"3"}"""));
    }

    [Fact]
    public async Task Multiple_choice_must_be_one_of_the_items_choices_and_is_stored_as_defined()
    {
        var resolver = Resolver();

        Assert.Equal("""{"choice":"Partially compliant"}""",
            await resolver.NormaliseValueAsync(ResponseType.MultipleChoice, ChoiceConfig, """{"choice":"partially COMPLIANT"}"""));

        await AssertInvalid(() => resolver.NormaliseValueAsync(ResponseType.MultipleChoice, ChoiceConfig, """{"choice":"Something else"}"""));
        // An item with no choices configured can accept nothing.
        await AssertInvalid(() => resolver.NormaliseValueAsync(ResponseType.MultipleChoice, null, """{"choice":"Anything"}"""));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    public async Task A_malformed_value_is_rejected(string valueJson)
    {
        await AssertInvalid(() => Resolver().NormaliseValueAsync(ResponseType.Text, null, valueJson));
    }

    private static async Task AssertInvalid(Func<Task<string?>> act)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(act);
        Assert.Equal("response.invalid_value", ex.Code);
    }
}
