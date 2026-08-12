using AuditX.Domain.Common;
using AuditX.Domain.Controls;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Controls;

public sealed class ControlTests
{
    private static Control New() => Control.Register(
        "CTL-1", "Dual authorisation", "desc", ControlType.Preventive, ControlFrequency.Continuous, Guid.NewGuid(), null);

    [Fact]
    public void A_new_control_starts_not_tested()
    {
        var control = New();
        Assert.Equal(ControlEffectiveness.NotTested, control.Effectiveness);
        Assert.Null(control.LastTestedDate);
    }

    [Fact]
    public void RecordTest_sets_effectiveness_and_last_tested_date()
    {
        var control = New();
        var date = new DateOnly(2027, 3, 1);
        control.RecordTest(ControlEffectiveness.Ineffective, date);
        Assert.Equal(ControlEffectiveness.Ineffective, control.Effectiveness);
        Assert.Equal(date, control.LastTestedDate);
    }

    [Fact]
    public void RecordTest_rejects_a_not_tested_result()
    {
        var control = New();
        Assert.Throws<DomainException>(() => control.RecordTest(ControlEffectiveness.NotTested, new DateOnly(2027, 3, 1)));
    }

    [Fact]
    public void ControlTest_factory_rejects_a_not_tested_result()
        => Assert.Throws<DomainException>(() =>
            ControlTest.Create(Guid.NewGuid(), null, null, ControlEffectiveness.NotTested, Guid.NewGuid(), DateTimeOffset.UnixEpoch, null));
}
