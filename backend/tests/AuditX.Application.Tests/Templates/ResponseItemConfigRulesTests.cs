using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Templates;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;
using NSubstitute;

namespace AuditX.Application.Tests.Templates;

/// <summary>
/// Every response type must be SAVED in a state an auditor can answer: a rating needs a real scale, a
/// multiple-choice item needs its choices, and types with no settings keep none.
/// </summary>
public sealed class ResponseItemConfigRulesTests
{
    private static readonly RatingScale Scale = RatingScale.Create(
        "Effectiveness", null, """[{"value":1,"label":"Weak","score":0},{"value":2,"label":"Strong","score":100}]""");

    private static IRatingScaleRepository Scales()
    {
        var repo = Substitute.For<IRatingScaleRepository>();
        repo.GetByIdAsync(Scale.Id, Arg.Any<CancellationToken>()).Returns(Scale);
        return repo;
    }

    private static Task<string?> Normalise(ResponseType type, string? json)
        => ResponseItemConfigRules.NormaliseAsync(type, json, Scales(), CancellationToken.None);

    [Theory]
    [InlineData(ResponseType.PassFailNa)]
    [InlineData(ResponseType.YesNo)]
    [InlineData(ResponseType.Text)]
    [InlineData(ResponseType.Date)]
    public async Task Types_without_settings_drop_whatever_was_sent(ResponseType type)
    {
        // e.g. an item switched from Rating to Yes/No must not keep the old scale reference.
        var result = await Normalise(type, $$"""{"ratingScaleId":"{{Scale.Id}}","options":["A","B"],"unit":"days"}""");

        Assert.Null(result);
    }

    [Fact]
    public async Task Rating_keeps_only_its_scale_reference()
    {
        var result = await Normalise(ResponseType.Rating, $$"""{"ratingScaleId":"{{Scale.Id}}","unit":"stale"}""");

        Assert.Equal($$"""{"ratingScaleId":"{{Scale.Id}}"}""", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("""{"max":5}""")] // the shape the audit checklist dialog used to send
    [InlineData("not json")]
    public async Task Rating_without_a_scale_is_rejected(string? json)
    {
        var ex = await Assert.ThrowsAsync<ConflictException>(() => Normalise(ResponseType.Rating, json));

        Assert.Equal("template.rating_scale_required", ex.ErrorCode);
    }

    [Fact]
    public async Task Rating_with_an_unknown_scale_is_rejected()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => Normalise(ResponseType.Rating, $$"""{"ratingScaleId":"{{Guid.NewGuid()}}"}"""));
    }

    [Fact]
    public async Task Multiple_choice_trims_and_drops_blank_choices()
    {
        var result = await Normalise(ResponseType.MultipleChoice, """{"options":["  Fully  ",""," Partially","   ","Not"]}""");

        Assert.Equal("""{"options":["Fully","Partially","Not"]}""", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{"options":[]}""")]
    [InlineData("""{"options":["Only one"]}""")]
    [InlineData("""{"options":["One","  "]}""")]
    public async Task Multiple_choice_needs_at_least_two_choices(string? json)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Normalise(ResponseType.MultipleChoice, json));

        Assert.Equal("response_config.choices_required", ex.Code);
    }

    [Fact]
    public async Task Multiple_choice_rejects_duplicates_regardless_of_case()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Normalise(ResponseType.MultipleChoice, """{"options":["Yes","No","yes"]}"""));

        Assert.Equal("response_config.duplicate_choice", ex.Code);
    }

    [Fact]
    public async Task Multiple_choice_caps_the_number_and_length_of_choices()
    {
        var tooMany = System.Text.Json.JsonSerializer.Serialize(new { options = Enumerable.Range(1, ResponseItemConfigRules.MaxChoices + 1).Select(i => $"Choice {i}") });
        var tooLong = System.Text.Json.JsonSerializer.Serialize(new { options = new[] { "ok", new string('x', ResponseItemConfigRules.MaxChoiceLength + 1) } });

        Assert.Equal("response_config.too_many_choices",
            (await Assert.ThrowsAsync<DomainException>(() => Normalise(ResponseType.MultipleChoice, tooMany))).Code);
        Assert.Equal("response_config.choice_too_long",
            (await Assert.ThrowsAsync<DomainException>(() => Normalise(ResponseType.MultipleChoice, tooLong))).Code);
    }

    [Fact]
    public async Task Numeric_keeps_a_trimmed_unit_and_nothing_when_blank()
    {
        Assert.Equal("""{"unit":"days"}""", await Normalise(ResponseType.Numeric, """{"unit":"  days "}"""));
        Assert.Null(await Normalise(ResponseType.Numeric, """{"unit":"   "}"""));
        Assert.Null(await Normalise(ResponseType.Numeric, null));
    }

    [Fact]
    public async Task Numeric_rejects_an_overlong_unit()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { unit = new string('u', ResponseItemConfigRules.MaxUnitLength + 1) });

        var ex = await Assert.ThrowsAsync<DomainException>(() => Normalise(ResponseType.Numeric, json));

        Assert.Equal("response_config.unit_too_long", ex.Code);
    }
}
