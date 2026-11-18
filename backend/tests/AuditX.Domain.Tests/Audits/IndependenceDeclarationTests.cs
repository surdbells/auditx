using AuditX.Domain.Audits;
using AuditX.Domain.Audits.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Audits;

/// <summary>
/// Independence: each member of the audit team declares that they are independent of the area, or discloses a
/// conflict of interest that a manager (never the declarant) accepts with safeguards. Where required, fieldwork waits
/// for the whole team; a member with an undecided conflict cannot work the papers; self-assessments are exempt.
/// </summary>
public sealed class IndependenceDeclarationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();
    private static readonly Guid Fieldworker = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static Audit Planned()
    {
        var a = Audit.Create("Treasury", "operational", new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 10),
            null, null, null, null, null, Lead, Auditee, null, Lead, Now);
        a.AddTeamMember(Fieldworker, TeamRole.Auditor, Lead, Now);
        a.AddChecklistItem("Dual control on vault?", null, ResponseType.PassFailNa, null, true, null);
        a.Plan();
        return a;
    }

    [Fact]
    public void Fieldwork_waits_for_every_member_of_the_audit_team_but_never_the_auditee()
    {
        var a = Planned();
        Assert.Equal(new[] { Lead, Fieldworker }.OrderBy(g => g), a.IndependenceOutstanding().OrderBy(g => g));

        a.DeclareIndependence(Lead, hasConflict: false, null, Now);
        var ex = Assert.Throws<DomainException>(() => a.Start(requireIndependence: true));
        Assert.Equal("audit.independence_outstanding", ex.Code);

        a.DeclareIndependence(Fieldworker, hasConflict: false, null, Now);
        Assert.Empty(a.IndependenceOutstanding());
        a.Start(requireIndependence: true);
        Assert.Equal(AuditStatus.InProgress, a.Status);
    }

    [Fact]
    public void Without_the_requirement_fieldwork_starts_as_before()
    {
        var a = Planned();
        a.Start(requireIndependence: false);
        Assert.Equal(AuditStatus.InProgress, a.Status);
    }

    [Fact]
    public void A_disclosed_conflict_holds_fieldwork_until_someone_else_accepts_it_with_safeguards()
    {
        var a = Planned();
        a.DeclareIndependence(Lead, hasConflict: false, null, Now);
        var declaration = a.DeclareIndependence(Fieldworker, hasConflict: true, "Worked in Treasury operations until last year.", Now);
        Assert.Equal(IndependenceStatus.ConflictDisclosed, declaration.Status);
        Assert.Contains(a.DomainEvents, e => e is IndependenceConflictDisclosedEvent d && d.UserId == Fieldworker && d.LeadUserId == Lead);
        Assert.Equal([Fieldworker], a.IndependenceOutstanding());

        var own = Assert.Throws<DomainException>(() => a.AcceptIndependenceConflict(declaration.Id, Fieldworker, "I will be careful.", Now));
        Assert.Equal("audit.accept_own_conflict", own.Code);
        var noSafeguards = Assert.Throws<DomainException>(() => a.AcceptIndependenceConflict(declaration.Id, Manager, "  ", Now));
        Assert.Equal("audit.safeguards_required", noSafeguards.Code);
        var byAuditee = Assert.Throws<DomainException>(() => a.AcceptIndependenceConflict(declaration.Id, Auditee, "Fine by me.", Now));
        Assert.Equal("audit.not_independent", byAuditee.Code);

        a.AcceptIndependenceConflict(declaration.Id, Manager, "The fieldworker does not test the treasury desk they worked on; the lead reviews all their items.", Now);
        Assert.Equal(IndependenceStatus.ConflictAccepted, declaration.Status);
        Assert.Equal(Manager, declaration.ReviewedBy);
        Assert.Empty(a.IndependenceOutstanding());
        a.Start(requireIndependence: true);
    }

    [Fact]
    public void A_conflict_needs_its_details_and_only_the_team_declares()
    {
        var a = Planned();
        var noDetails = Assert.Throws<DomainException>(() => a.DeclareIndependence(Fieldworker, hasConflict: true, " ", Now));
        Assert.Equal("audit.conflict_details_required", noDetails.Code);

        foreach (var notOnTeam in new[] { Auditee, Manager })
        {
            var ex = Assert.Throws<DomainException>(() => a.DeclareIndependence(notOnTeam, hasConflict: false, null, Now));
            Assert.Equal("audit.not_on_team", ex.Code);
        }
    }

    [Fact]
    public void A_member_with_an_undecided_conflict_cannot_answer_or_sign_off_until_it_is_accepted()
    {
        var a = Planned();
        a.Start(requireIndependence: false);
        var item = a.ChecklistItems.Single().Id;

        // Circumstances change during fieldwork: the fieldworker discloses a conflict.
        var declaration = a.DeclareIndependence(Fieldworker, hasConflict: true, "My partner joined the treasury desk last month.", Now);
        var answer = Assert.Throws<DomainException>(() =>
            a.RecordResponse(item, ResponseVerdict.Pass, null, isDraft: false, Fieldworker, requireCommentOnPass: false, Now));
        Assert.Equal("audit.independence_conflict_pending", answer.Code);

        a.AcceptIndependenceConflict(declaration.Id, Manager, "The lead re-performs a sample of their testing.", Now);
        a.RecordResponse(item, ResponseVerdict.Pass, null, isDraft: false, Fieldworker, requireCommentOnPass: false, Now);
    }

    [Fact]
    public void Declaring_again_replaces_the_earlier_declaration_and_its_decision()
    {
        var a = Planned();
        var declaration = a.DeclareIndependence(Fieldworker, hasConflict: true, "Former role in the area.", Now);
        a.AcceptIndependenceConflict(declaration.Id, Manager, "Second reviewer on their items.", Now);

        var again = a.DeclareIndependence(Fieldworker, hasConflict: false, null, Now.AddDays(1));
        Assert.Same(declaration, again);
        Assert.Single(a.IndependenceDeclarations);
        Assert.Equal(IndependenceStatus.Declared, again.Status);
        Assert.Null(again.ConflictDetails);
        Assert.Null(again.ReviewedBy);
        Assert.Equal(Now.AddDays(1), again.DeclaredAt);
    }

    [Fact]
    public void A_self_assessment_has_no_declarations_and_starts_without_them()
    {
        var a = Audit.Create("Self check", "operational", new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 10),
            null, null, null, null, null, Lead, Lead, null, Lead, Now, isSelfAssessment: true);
        a.AddChecklistItem("Reconciliations reviewed?", null, ResponseType.PassFailNa, null, true, null);
        a.Plan();

        Assert.Empty(a.IndependenceOutstanding());
        var ex = Assert.Throws<DomainException>(() => a.DeclareIndependence(Lead, hasConflict: false, null, Now));
        Assert.Equal("audit.independence_not_applicable", ex.Code);
        a.Start(requireIndependence: true);
    }

    [Fact]
    public void Declarations_close_once_the_audit_is_completed_or_cancelled()
    {
        var a = Planned();
        a.Cancel("The area was restructured, so the engagement no longer applies.", Now);
        Assert.Throws<InvalidStateTransitionException>(() => a.DeclareIndependence(Fieldworker, hasConflict: false, null, Now));
    }
}
