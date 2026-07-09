using AuditX.Application.Reports.Commands;

namespace AuditX.Application.Tests.Reports;

public sealed class DistributeReportValidatorTests
{
    private static readonly DistributeReportCommandValidator Validator = new();

    [Fact]
    public void Accepts_roles_as_the_only_recipient_type()
    {
        var result = Validator.Validate(new DistributeReportCommand(Guid.NewGuid(), [], [], ["Audit Manager"]));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_when_no_recipient_of_any_kind_is_supplied()
    {
        var result = Validator.Validate(new DistributeReportCommand(Guid.NewGuid(), [], [], []));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "report.recipients_required");
    }
}
