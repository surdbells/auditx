using AuditX.Domain.Audits;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Audits;

/// <summary>
/// Independence on the audit aggregate itself, for the scenario the product owner posed: five auditors on the
/// platform; on "Audit A" two of them are its auditors and the other three are its auditees. Whatever those three can
/// do elsewhere, on Audit A they are on the audited side — and nothing on the aggregate lets them cross over.
/// </summary>
public sealed class AuditIndependenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Start = new(2027, 1, 10);
    private static readonly DateOnly End = new(2027, 2, 10);

    private static readonly Guid Lead = Guid.NewGuid();

    // The five auditors.
    private static readonly Guid AuditorOne = Guid.NewGuid();
    private static readonly Guid AuditorTwo = Guid.NewGuid();
    private static readonly Guid AuditeeOne = Guid.NewGuid(); // primary auditee
    private static readonly Guid AuditeeTwo = Guid.NewGuid();
    private static readonly Guid AuditeeThree = Guid.NewGuid();

    private static readonly Guid[] Auditees = [AuditeeOne, AuditeeTwo, AuditeeThree];
    private static readonly Guid[] Auditors = [AuditorOne, AuditorTwo];

    /// <summary>Audit A in draft: two auditors, three auditees.</summary>
    private static Audit AuditA()
    {
        var audit = Audit.Create("Audit A", "branch", Start, End, null, null, null, null, null, Lead, AuditeeOne, null, Lead, Now);
        audit.AddTeamMember(AuditorOne, TeamRole.Auditor, Lead, Now);
        audit.AddTeamMember(AuditorTwo, TeamRole.Auditor, Lead, Now);
        audit.AddTeamMember(AuditeeTwo, TeamRole.Auditee, Lead, Now);
        audit.AddTeamMember(AuditeeThree, TeamRole.Auditee, Lead, Now);
        return audit;
    }

    [Fact]
    public void An_audit_can_have_several_auditees_and_each_is_on_the_audited_side()
    {
        var audit = AuditA();

        Assert.All(Auditees, auditee => Assert.True(audit.IsAuditee(auditee)));
        Assert.All(Auditors, auditor => Assert.False(audit.IsAuditee(auditor)));
        Assert.False(audit.IsAuditee(Lead));
        Assert.Equal(3, audit.TeamMembers.Count(m => m.IsActive && m.TeamRole == TeamRole.Auditee));
    }

    [Theory]
    [InlineData(TeamRole.Auditor)]
    [InlineData(TeamRole.Reviewer)]
    [InlineData(TeamRole.Lead)]
    public void An_auditee_can_never_be_put_on_the_audit_team(TeamRole workingRole)
    {
        var audit = AuditA();

        foreach (var auditee in Auditees)
        {
            var refused = Assert.Throws<DomainException>(() => audit.AddTeamMember(auditee, workingRole, Lead, Now));
            Assert.Equal("audit.member_is_auditee", refused.Code);
            Assert.True(audit.IsAuditee(auditee)); // still an auditee, and only an auditee
            Assert.Single(audit.TeamMembers, m => m.IsActive && m.UserId == auditee);
        }
    }

    [Fact]
    public void Someone_on_the_audit_team_can_never_also_be_named_an_auditee()
    {
        var audit = AuditA();

        foreach (var member in Auditors.Append(Lead))
        {
            var refused = Assert.Throws<DomainException>(() => audit.AddTeamMember(member, TeamRole.Auditee, Lead, Now));
            Assert.Equal("audit.member_on_audit_team", refused.Code);
            Assert.False(audit.IsAuditee(member));
        }
    }

    [Fact]
    public void An_auditee_cannot_be_made_the_lead()
    {
        var audit = AuditA();
        Assert.All(Auditees, auditee => Assert.Throws<DomainException>(() => audit.TransferLead(auditee, removeOutgoing: false, Now)));
        Assert.Equal(Lead, audit.LeadUserId);
    }

    [Fact]
    public void A_checklist_item_can_be_assigned_to_an_auditor_but_never_to_an_auditee()
    {
        var audit = AuditA();
        var item = audit.AddChecklistItem("Is dual control enforced?", null, ResponseType.PassFailNa, null, true, null);
        audit.Plan();
        audit.Start(requireIndependence: false);

        foreach (var auditee in Auditees)
        {
            var refused = Assert.Throws<DomainException>(() => audit.AssignItem(item.Id, auditee));
            Assert.Equal("audit.assignee_is_auditee", refused.Code);
        }

        audit.AssignItem(item.Id, AuditorOne);
        Assert.Equal(AuditorOne, audit.ChecklistItems.Single().AssignedUserId);
    }

    [Fact]
    public void Sides_are_fixed_once_the_audit_has_started()
    {
        var audit = AuditA();
        audit.AddChecklistItem("Is dual control enforced?", null, ResponseType.PassFailNa, null, true, null);
        audit.Plan();
        audit.Start(requireIndependence: false);

        // Nobody can be moved on, off, or across the audit once fieldwork is under way.
        var extra = audit.TeamMembers.Single(m => m.UserId == AuditeeTwo);
        Assert.Throws<InvalidStateTransitionException>(() => audit.RemoveTeamMember(extra.Id, Now));
        Assert.Throws<InvalidStateTransitionException>(() => audit.AddTeamMember(Guid.NewGuid(), TeamRole.Auditor, Lead, Now));
        Assert.Throws<InvalidStateTransitionException>(() => audit.AddTeamMember(AuditeeTwo, TeamRole.Auditor, Lead, Now));
    }

    [Fact]
    public void An_additional_auditee_can_be_removed_but_the_primary_auditee_cannot()
    {
        var audit = AuditA();

        var extra = audit.TeamMembers.Single(m => m.UserId == AuditeeThree);
        audit.RemoveTeamMember(extra.Id, Now);
        Assert.False(audit.IsAuditee(AuditeeThree));

        var primary = audit.TeamMembers.Single(m => m.UserId == AuditeeOne);
        var refused = Assert.Throws<DomainException>(() => audit.RemoveTeamMember(primary.Id, Now));
        Assert.Equal("audit.cannot_remove_sole_auditee", refused.Code);
        Assert.True(audit.IsAuditee(AuditeeOne));
    }

    [Fact]
    public void Adding_someone_again_on_the_same_side_changes_nothing()
    {
        var audit = AuditA();
        var before = audit.TeamMembers.Count;

        audit.AddTeamMember(AuditeeTwo, TeamRole.Auditee, Lead, Now);
        audit.AddTeamMember(AuditorOne, TeamRole.Auditor, Lead, Now);

        Assert.Equal(before, audit.TeamMembers.Count);
    }

    [Fact]
    public void A_self_assessment_has_no_auditees()
    {
        var assessor = Guid.NewGuid();
        var selfAssessment = Audit.Create("Self-assessment", "branch", Start, End, null, null, null, null, null,
            assessor, assessor, null, assessor, Now, isSelfAssessment: true);

        Assert.False(selfAssessment.IsAuditee(assessor)); // the assessor does the work — by design
        Assert.Throws<DomainException>(() => selfAssessment.AddTeamMember(Guid.NewGuid(), TeamRole.Auditee, assessor, Now));
    }
}
