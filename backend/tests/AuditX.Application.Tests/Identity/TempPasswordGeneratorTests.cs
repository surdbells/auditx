using AuditX.Application.Identity.Passwords;
using AuditX.Application.Identity.Services;
using AuditX.Domain.Identity;

namespace AuditX.Application.Tests.Identity;

public sealed class TempPasswordGeneratorTests
{
    [Fact]
    public void Generated_password_satisfies_the_default_strict_policy()
    {
        var settings = InstitutionSettings.CreateDefault("Acme"); // 12 chars, all classes required
        for (var i = 0; i < 50; i++)
        {
            var pw = TempPasswordGenerator.Generate(settings);
            Assert.True(pw.Length >= 16);
            Assert.True(PasswordPolicyEvaluator.IsSatisfiedBy(PasswordService.SpecFrom(settings), pw), $"failed for: {pw}");
        }
    }

    [Fact]
    public void Generated_password_respects_a_longer_minimum_length()
    {
        var settings = InstitutionSettings.CreateDefault("Acme");
        settings.SetPasswordPolicy(true, 40, true, true, true, true, 5, 90, 5, 15);
        var pw = TempPasswordGenerator.Generate(settings);
        Assert.True(pw.Length >= 40);
        Assert.True(PasswordPolicyEvaluator.IsSatisfiedBy(PasswordService.SpecFrom(settings), pw));
    }

    [Fact]
    public void Generated_password_is_random_each_time()
    {
        var settings = InstitutionSettings.CreateDefault("Acme");
        Assert.NotEqual(TempPasswordGenerator.Generate(settings), TempPasswordGenerator.Generate(settings));
    }
}
