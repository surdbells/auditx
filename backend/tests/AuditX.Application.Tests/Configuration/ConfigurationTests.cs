using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Configuration;
using AuditX.Application.Configuration.Commands;
using AuditX.Domain.Common;
using AuditX.Domain.Configuration;
using NSubstitute;

namespace AuditX.Application.Tests.Configuration;

public sealed class ConfigurationDefinitionsTests
{
    private static string ValidJson() => ConfigurationDefinitions.SerializeExceptionDefaults(ExceptionDefaultsDefinition.HardcodedFallback);

    [Fact]
    public void Valid_exception_defaults_round_trips()
    {
        var parsed = ConfigurationDefinitions.ParseExceptionDefaults(ValidJson());
        Assert.Equal(14, parsed.CriticalTargetDays);
        Assert.Equal(24, parsed.RecurrenceWindowMonths);
        Assert.Equal(3, parsed.RecurrenceThreshold);
    }

    [Fact]
    public void Negative_target_days_are_rejected()
    {
        var json = ConfigurationDefinitions.SerializeExceptionDefaults(
            ExceptionDefaultsDefinition.HardcodedFallback with { HighTargetDays = -1 });
        Assert.Throws<DomainException>(() => ConfigurationDefinitions.ParseExceptionDefaults(json));
    }

    [Fact]
    public void Window_out_of_range_is_rejected()
    {
        var json = ConfigurationDefinitions.SerializeExceptionDefaults(
            ExceptionDefaultsDefinition.HardcodedFallback with { RecurrenceWindowMonths = 999 });
        Assert.Throws<DomainException>(() => ConfigurationDefinitions.ParseExceptionDefaults(json));
    }

    [Fact]
    public void Threshold_below_two_is_rejected()
    {
        var json = ConfigurationDefinitions.SerializeExceptionDefaults(
            ExceptionDefaultsDefinition.HardcodedFallback with { RecurrenceThreshold = 1 });
        Assert.Throws<DomainException>(() => ConfigurationDefinitions.ParseExceptionDefaults(json));
    }

    [Fact]
    public void Malformed_json_is_rejected()
        => Assert.Throws<DomainException>(() => ConfigurationDefinitions.ParseExceptionDefaults("{ not json"));

    [Fact]
    public void Validate_rejects_an_unknown_domain()
        => Assert.Throws<DomainException>(() => ConfigurationDefinitions.Validate("not_a_domain", ValidJson()));
}

public sealed class CreateConfigurationDraftCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Reason = "Tightening the remediation SLA for FY27";

    [Fact]
    public async Task Create_mints_the_next_version_number_as_an_inactive_draft()
    {
        var json = ConfigurationDefinitions.SerializeExceptionDefaults(ExceptionDefaultsDefinition.HardcodedFallback);
        var repo = Substitute.For<IInstitutionConfigurationRepository>();
        repo.GetMaxVersionNumberAsync(ConfigurationDomains.ExceptionDefaults, Arg.Any<CancellationToken>()).Returns(2);
        InstitutionConfiguration? added = null;
        repo.When(r => r.Add(Arg.Any<InstitutionConfiguration>())).Do(ci => added = ci.Arg<InstitutionConfiguration>());

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var handler = new CreateConfigurationDraftCommandHandler(
            repo, currentUser, Substitute.For<IAuditRecorder>(), clock, Substitute.For<IUnitOfWork>());

        var result = await handler.Handle(
            new CreateConfigurationDraftCommand(ConfigurationDomains.ExceptionDefaults, json, Reason), default);

        Assert.NotNull(added);
        Assert.Equal(3, added!.VersionNumber);
        Assert.False(added.IsActive);
        Assert.NotNull(result.Version);
        Assert.Null(result.PendingActionId);
        repo.Received(1).Add(Arg.Any<InstitutionConfiguration>());
    }

    [Fact]
    public async Task Create_rejects_an_unknown_domain()
    {
        var handler = new CreateConfigurationDraftCommandHandler(
            Substitute.For<IInstitutionConfigurationRepository>(), AuthedUser(), Substitute.For<IAuditRecorder>(),
            Substitute.For<IClock>(), Substitute.For<IUnitOfWork>());

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new CreateConfigurationDraftCommand("not_a_domain", "{}", Reason), default));
    }

    private static ICurrentUser AuthedUser()
    {
        var u = Substitute.For<ICurrentUser>();
        u.UserId.Returns(Guid.NewGuid());
        return u;
    }
}

public sealed class ConfigurationValidatorTests
{
    [Fact]
    public void Create_validator_requires_a_change_reason_of_at_least_20_chars()
    {
        var validator = new CreateConfigurationDraftCommandValidator();
        var result = validator.Validate(new CreateConfigurationDraftCommand("exception_defaults", "{}", "too short"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Activate_validator_requires_a_change_reason()
    {
        var validator = new ActivateConfigurationVersionCommandValidator();
        Assert.False(validator.Validate(new ActivateConfigurationVersionCommand("exception_defaults", 2, "short")).IsValid);
    }
}
