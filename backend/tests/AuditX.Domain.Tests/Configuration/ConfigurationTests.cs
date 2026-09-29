using AuditX.Domain.Common;
using AuditX.Domain.Configuration;
using AuditX.Domain.Configuration.Events;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Configuration;

public sealed class InstitutionConfigurationTests
{
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Json = "{\"criticalTargetDays\":14}";
    private const string Reason = "Tightening the remediation SLA for FY27";

    [Fact]
    public void CreateDraft_is_inactive_and_raises_the_created_event()
    {
        var c = InstitutionConfiguration.CreateDraft(ConfigurationDomains.ExceptionDefaults, 1, Json, Reason, Guid.NewGuid(), Now);

        Assert.False(c.IsActive);
        Assert.Equal(1, c.VersionNumber);
        Assert.Contains(c.DomainEvents, e => e is ConfigurationVersionCreatedEvent);
    }

    [Fact]
    public void CreateDraft_rejects_a_short_change_reason()
        => Assert.Throws<DomainException>(() =>
            InstitutionConfiguration.CreateDraft(ConfigurationDomains.ExceptionDefaults, 1, Json, "too short", Guid.NewGuid(), Now));

    [Fact]
    public void Activate_sets_active_and_raises_the_activated_event()
    {
        var c = InstitutionConfiguration.CreateDraft(ConfigurationDomains.ExceptionDefaults, 1, Json, Reason, Guid.NewGuid(), Now);
        c.ClearDomainEvents();

        c.Activate(Guid.NewGuid(), Now);

        Assert.True(c.IsActive);
        Assert.NotNull(c.ActivatedAt);
        Assert.Contains(c.DomainEvents, e => e is ConfigurationVersionActivatedEvent);
    }

    [Fact]
    public void Deactivate_clears_the_active_flag()
    {
        var c = InstitutionConfiguration.CreateDraft(ConfigurationDomains.ExceptionDefaults, 1, Json, Reason, Guid.NewGuid(), Now);
        c.Activate(Guid.NewGuid(), Now);

        c.Deactivate();

        Assert.False(c.IsActive);
    }
}

public sealed class ExceptionDefaultsDefinitionTests
{
    [Fact]
    public void HardcodedFallback_reproduces_the_pre_m12_values_exactly()
    {
        var f = ExceptionDefaultsDefinition.HardcodedFallback;
        Assert.Equal(14, f.CriticalTargetDays);
        Assert.Equal(30, f.HighTargetDays);
        Assert.Equal(45, f.MediumTargetDays);
        Assert.Equal(60, f.LowTargetDays);
        Assert.Equal(24, f.RecurrenceWindowMonths);
        Assert.Equal(3, f.RecurrenceThreshold);
    }

    [Theory]
    [InlineData(ExceptionSeverity.Critical, 14)]
    [InlineData(ExceptionSeverity.High, 30)]
    [InlineData(ExceptionSeverity.Medium, 45)]
    [InlineData(ExceptionSeverity.Low, 60)]
    public void TargetDays_maps_each_severity(ExceptionSeverity severity, int expected)
        => Assert.Equal(expected, ExceptionDefaultsDefinition.HardcodedFallback.TargetDays(severity));

    [Fact]
    public void Unknown_domain_is_rejected_by_the_catalogue()
    {
        Assert.True(ConfigurationDomains.IsKnown(ConfigurationDomains.ExceptionDefaults));
        Assert.False(ConfigurationDomains.IsKnown("not_a_domain"));
    }
}
