using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Domain.Tests.Exceptions;

public sealed class AuditExceptionFinancialImpactTests
{
    private static AuditException Raise() => AuditException.Raise(
        Guid.NewGuid(), Guid.NewGuid(), null, "Finding", ExceptionSeverity.High,
        "root", "reco", null, Guid.NewGuid(), Guid.NewGuid(),
        new DateOnly(2026, 8, 1), targetDateOverridden: false, overrideRationale: null,
        isRecurrence: false, recurrenceOfExceptionId: null, configurationVersionsJson: null,
        nowUtc: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Set_records_amount_and_normalises_currency()
    {
        var e = Raise();
        e.SetFinancialImpact(1_000_000m, " ngn ");

        Assert.Equal(1_000_000m, e.FinancialImpact);
        Assert.Equal("NGN", e.FinancialImpactCurrency);
    }

    [Fact]
    public void Set_with_null_amount_clears_both_fields()
    {
        var e = Raise();
        e.SetFinancialImpact(500m, "USD");
        e.SetFinancialImpact(null, "USD");

        Assert.Null(e.FinancialImpact);
        Assert.Null(e.FinancialImpactCurrency);
    }

    [Fact]
    public void Set_rejects_a_negative_amount()
    {
        var e = Raise();
        Assert.Throws<DomainException>(() => e.SetFinancialImpact(-1m, "NGN"));
    }

    [Fact]
    public void A_new_finding_has_no_financial_impact()
    {
        var e = Raise();
        Assert.Null(e.FinancialImpact);
        Assert.Null(e.FinancialImpactCurrency);
    }
}
