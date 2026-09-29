using AuditX.Application.Identity.Passwords;

namespace AuditX.Application.Tests.Identity;

public sealed class PasswordPolicyEvaluatorTests
{
    private static readonly PasswordComplexitySpec Strict = new(
        MinLength: 12, RequireUppercase: true, RequireLowercase: true, RequireDigit: true, RequireSymbol: true);

    [Fact]
    public void Accepts_a_password_that_meets_every_requirement()
    {
        var errors = PasswordPolicyEvaluator.Validate(Strict, "Str0ng&Pass!");
        Assert.Empty(errors);
        Assert.True(PasswordPolicyEvaluator.IsSatisfiedBy(Strict, "Str0ng&Pass!"));
    }

    [Fact]
    public void Rejects_a_null_or_empty_password_with_a_single_error()
    {
        Assert.Single(PasswordPolicyEvaluator.Validate(Strict, null));
        Assert.Single(PasswordPolicyEvaluator.Validate(Strict, string.Empty));
    }

    [Fact]
    public void Flags_a_too_short_password()
    {
        var errors = PasswordPolicyEvaluator.Validate(Strict, "Aa1!aa");
        Assert.Contains(errors, e => e.Contains("at least 12", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("alllowercase1!aa", "uppercase")]
    [InlineData("ALLUPPERCASE1!AA", "lowercase")]
    [InlineData("NoDigitsHere!!aa", "digit")]
    [InlineData("NoSymbolsHere1aa", "symbol")]
    public void Flags_each_missing_character_class(string password, string expectedFragment)
    {
        var errors = PasswordPolicyEvaluator.Validate(Strict, password);
        Assert.Contains(errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Relaxed_policy_only_enforces_length()
    {
        var relaxed = new PasswordComplexitySpec(8, false, false, false, false);
        Assert.Empty(PasswordPolicyEvaluator.Validate(relaxed, "plainlowercase"));
        Assert.NotEmpty(PasswordPolicyEvaluator.Validate(relaxed, "short"));
    }
}
