using AuditX.Domain.Audits;
using AuditX.Domain.Audits.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Audits;

/// <summary>Scope, objectives and approval of the audit programme before fieldwork.</summary>
public sealed class ProgrammeApprovalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Start = new(2027, 1, 10);
    private static readonly DateOnly End = new(2027, 2, 10);
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();
    private static readonly Guid Fieldworker = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static Audit Draft()
    {
        var a = Audit.Create("Branch Audit", "branch", Start, End, "Cash handling", null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddTeamMember(Fieldworker, TeamRole.Auditor, Lead, Now);
        a.AddChecklistItem("Cash counted?", null, ResponseType.PassFailNa, null, true, null);
        return a;
    }

    private static Audit Planned()
    {
        var a = Draft();
        a.Plan();
        a.ClearDomainEvents();
        return a;
    }

    [Fact]
    public void Scope_and_objectives_are_edited_while_draft_or_planned_and_trimmed()
    {
        var a = Draft();
        a.UpdateMetadata("Branch Audit", "  Cash and vault  ", Start, End, "  Confirm dual control over the vault.  ");
        Assert.Equal("Cash and vault", a.ScopeDescription);
        Assert.Equal("Confirm dual control over the vault.", a.Objectives);

        a.UpdateMetadata("Branch Audit", "   ", Start, End, null);
        Assert.Null(a.ScopeDescription);
        Assert.Null(a.Objectives);

        Assert.Equal("audit.objectives_too_long", Assert.Throws<DomainException>(
            () => a.UpdateMetadata("Branch Audit", null, Start, End, new string('x', 4001))).Code);
    }

    [Fact]
    public void A_manager_other_than_the_lead_approves_the_planned_programme()
    {
        var a = Planned();

        Assert.Equal("audit.programme_self_approval",
            Assert.Throws<DomainException>(() => a.ApproveProgramme(Lead, null, Now)).Code);

        a.ApproveProgramme(Manager, "  Cover the vault keys too.  ", Now);
        Assert.True(a.ProgrammeApproved);
        Assert.Equal(Manager, a.ProgrammeApprovedBy);
        Assert.Equal("Cover the vault keys too.", a.ProgrammeApprovalNote);
        var approved = Assert.Single(a.DomainEvents.OfType<AuditProgrammeApprovedEvent>());
        Assert.Equal(Lead, approved.LeadUserId);

        Assert.Throws<InvalidStateTransitionException>(() => a.ApproveProgramme(Manager, null, Now));
    }

    [Fact]
    public void Only_a_planned_programme_can_be_approved()
    {
        Assert.Throws<InvalidStateTransitionException>(() => Draft().ApproveProgramme(Manager, null, Now));
    }

    [Fact]
    public void Fieldwork_waits_for_approval_where_required()
    {
        var a = Planned();
        Assert.Equal("audit.programme_not_approved",
            Assert.Throws<DomainException>(() => a.Start(requireIndependence: false, requireProgrammeApproval: true)).Code);

        a.ApproveProgramme(Manager, null, Now);
        a.Start(requireIndependence: false, requireProgrammeApproval: true);
        Assert.Equal(AuditStatus.InProgress, a.Status);
    }

    [Fact]
    public void Without_the_requirement_fieldwork_starts_unapproved()
    {
        var a = Planned();
        a.Start(requireIndependence: false, requireProgrammeApproval: false);
        Assert.Equal(AuditStatus.InProgress, a.Status);
    }

    [Fact]
    public void An_approved_scope_is_fixed_until_the_audit_goes_back_to_draft_which_withdraws_the_approval()
    {
        var a = Planned();
        a.UpdateMetadata("Branch Audit", "Cash handling", Start, End, "Objective one");
        a.ApproveProgramme(Manager, null, Now);

        // Dates and name may still move; the approved scope and objectives may not.
        a.UpdateMetadata("Branch Audit (Lagos)", "Cash handling", Start, End.AddDays(5), "Objective one");
        Assert.Equal("audit.programme_approved", Assert.Throws<InvalidStateTransitionException>(
            () => a.UpdateMetadata("Branch Audit", "Cash and loans", Start, End, "Objective one")).Code);

        a.Reopen("The scope needs to include loans.");
        Assert.False(a.ProgrammeApproved);
        Assert.Null(a.ProgrammeApprovedBy);
        a.UpdateMetadata("Branch Audit", "Cash and loans", Start, End, "Objective one");
        Assert.Equal("Cash and loans", a.ScopeDescription);
    }

    [Fact]
    public void A_self_assessment_has_no_programme_to_approve_and_is_never_held()
    {
        var a = Audit.Create("Self review", "branch", Start, End, null, null, null, null, null, Lead, Lead, null, Lead, Now, isSelfAssessment: true);
        a.AddChecklistItem("Q", null, ResponseType.PassFailNa, null, true, null);
        a.Plan();

        Assert.Throws<DomainException>(() => a.ApproveProgramme(Manager, null, Now));
        a.Start(requireIndependence: true, requireProgrammeApproval: true);
        Assert.Equal(AuditStatus.InProgress, a.Status);
    }
}
