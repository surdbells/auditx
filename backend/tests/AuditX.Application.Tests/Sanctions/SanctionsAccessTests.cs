using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Sanctions;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions;
using NSubstitute;

namespace AuditX.Application.Tests.Sanctions;

public sealed class SanctionsAccessTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static SanctionsCase CaseFor(Guid subject, Guid triggeredBy)
        => SanctionsCase.Trigger(Guid.NewGuid(), subject, "cash_handling", ExceptionSeverity.High, false, triggeredBy, Now);

    private static SanctionsCase SubmittedCase(Guid subject)
    {
        var c = CaseFor(subject, Guid.NewGuid());
        c.RecordRecommendation("Written warning", 1, "Written warning", true, null, Guid.NewGuid(), Now);
        c.SubmitRecommendation(Guid.NewGuid(), Now);
        return c;
    }

    [Fact]
    public async Task The_subject_is_always_a_team_member()
    {
        var subject = Guid.NewGuid();
        var c = CaseFor(subject, Guid.NewGuid());
        var perms = Substitute.For<IPermissionResolver>();

        Assert.True(await SanctionsAccess.IsCaseTeamMemberAsync(c, subject, perms, default));
    }

    [Fact]
    public async Task An_unrelated_user_without_permissions_is_not_a_team_member()
    {
        var c = CaseFor(Guid.NewGuid(), Guid.NewGuid());
        var perms = Substitute.For<IPermissionResolver>();
        perms.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(false);

        Assert.False(await SanctionsAccess.IsCaseTeamMemberAsync(c, Guid.NewGuid(), perms, default));
    }

    [Fact]
    public async Task An_hr_holder_gains_access_only_while_the_case_awaits_hr()
    {
        var hr = Guid.NewGuid();
        var perms = Substitute.For<IPermissionResolver>();
        perms.HasPermissionAsync(hr, PermissionKeys.RecordHrOutcome, null, Arg.Any<CancellationToken>()).Returns(true);

        var submitted = SubmittedCase(Guid.NewGuid());
        Assert.True(await SanctionsAccess.IsCaseTeamMemberAsync(submitted, hr, perms, default));

        var drafting = CaseFor(Guid.NewGuid(), Guid.NewGuid()); // recommendation_drafted — before HR's window
        Assert.False(await SanctionsAccess.IsCaseTeamMemberAsync(drafting, hr, perms, default));
    }

    [Fact]
    public async Task A_dc_member_gains_access_only_once_the_case_is_referred()
    {
        var dc = Guid.NewGuid();
        var perms = Substitute.For<IPermissionResolver>();
        perms.HasPermissionAsync(dc, PermissionKeys.DcMember, null, Arg.Any<CancellationToken>()).Returns(true);

        var submitted = SubmittedCase(Guid.NewGuid());
        Assert.False(await SanctionsAccess.IsCaseTeamMemberAsync(submitted, dc, perms, default));

        submitted.ReferToDc("This case warrants disciplinary committee review.", Guid.NewGuid(), Now);
        Assert.True(await SanctionsAccess.IsCaseTeamMemberAsync(submitted, dc, perms, default));
    }
}
