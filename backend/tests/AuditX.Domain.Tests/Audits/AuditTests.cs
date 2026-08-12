using AuditX.Domain.Audits;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Tests.Audits;

public sealed class AuditTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly DateOnly Start = new(2027, 1, 10);
    private static readonly DateOnly End = new(2027, 2, 10);
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();

    private static Audit New() => Audit.Create("Branch Audit", "branch", Start, End, null, null, null, null, null, Lead, Auditee, null, Lead, Now);

    private static Audit ReadyToPlan()
    {
        var a = New();
        a.AddTeamMember(Guid.NewGuid(), TeamRole.Auditor, Lead, Now);
        a.AddChecklistItem("Cash counted?", null, ResponseType.PassFailNa, null, true, null);
        return a;
    }

    [Fact]
    public void Create_rejects_target_before_start()
        => Assert.Throws<DomainException>(() => Audit.Create("X", "t", End, Start, null, null, null, null, null, Lead, Auditee, null, Lead, Now));

    [Fact]
    public void Create_rejects_lead_equal_auditee()
        => Assert.Throws<DomainException>(() => Audit.Create("X", "t", Start, End, null, null, null, null, null, Lead, Lead, null, Lead, Now));

    [Fact]
    public void Create_starts_draft_with_lead_and_auditee()
    {
        var a = New();
        Assert.Equal(AuditStatus.Draft, a.Status);
        Assert.Equal(2, a.TeamMembers.Count);
        Assert.Contains(a.DomainEvents, e => e is Domain.Audits.Events.AuditCreatedEvent);
    }

    [Fact]
    public void Plan_requires_checklist_and_auditor()
    {
        var empty = New();
        Assert.Throws<DomainException>(empty.Plan); // no checklist + no auditor

        var noAuditor = New();
        noAuditor.AddChecklistItem("Q", null, ResponseType.PassFailNa, null, true, null);
        var ex = Assert.Throws<DomainException>(noAuditor.Plan);
        Assert.Equal("audit.no_auditor", ex.Code);
    }

    private static Audit NewSelfAssessment()
        => Audit.Create("Self review", "branch", Start, End, null, null, null, null, null, Lead, Lead, null, Lead, Now, isSelfAssessment: true);

    [Fact]
    public void Self_assessment_allows_lead_equal_auditee_and_adds_a_single_member()
    {
        var a = NewSelfAssessment();
        Assert.True(a.IsSelfAssessment);
        Assert.Equal(Lead, a.LeadUserId);
        Assert.Equal(Lead, a.AuditeeUserId);
        Assert.Single(a.TeamMembers); // one Lead member, not a duplicated Lead + Auditee
        Assert.Equal(TeamRole.Lead, a.TeamMembers[0].TeamRole);
    }

    [Fact]
    public void Self_assessment_rejects_distinct_lead_and_auditee()
        => Assert.Throws<DomainException>(() =>
            Audit.Create("X", "t", Start, End, null, null, null, null, null, Lead, Auditee, null, Lead, Now, isSelfAssessment: true));

    [Fact]
    public void Self_assessment_plans_without_an_auditor()
    {
        var a = NewSelfAssessment();
        a.AddChecklistItem("Q", null, ResponseType.PassFailNa, null, true, null);
        a.Plan(); // no auditor added — the assessor is the only member
        Assert.Equal(AuditStatus.Planned, a.Status);
    }

    [Fact]
    public void Self_assessment_still_requires_a_checklist_item_to_plan()
    {
        var a = NewSelfAssessment();
        var ex = Assert.Throws<DomainException>(a.Plan);
        Assert.Equal("audit.checklist_empty", ex.Code);
    }

    [Fact]
    public void Full_happy_path_to_completed()
    {
        var a = ReadyToPlan();
        a.Plan();
        Assert.Equal(AuditStatus.Planned, a.Status);
        a.Start();
        Assert.Equal(AuditStatus.InProgress, a.Status);

        // Unanswered items require a reason to move to review.
        Assert.Throws<DomainException>(() => a.SendToReview(null));
        a.ChecklistItems[0].SetState(ChecklistItemState.Responded);
        a.SendToReview(null);
        Assert.Equal(AuditStatus.UnderReview, a.Status);

        a.Complete(new DateOnly(2027, 2, 5));
        Assert.Equal(AuditStatus.Completed, a.Status);
        Assert.Equal(new DateOnly(2027, 2, 5), a.ActualEndDate);
        Assert.Contains(a.DomainEvents, e => e is Domain.Audits.Events.AuditCompletedEvent);
    }

    [Fact]
    public void Return_to_in_progress_from_review_requires_reason()
    {
        var a = ReadyToPlan();
        a.Plan();
        a.Start();
        a.SendToReview("submitting for review");
        Assert.Equal(AuditStatus.UnderReview, a.Status);

        Assert.Throws<DomainException>(() => a.ReturnToInProgress(" "));
        a.ReturnToInProgress("Reviewer requested rework on the cash item.");
        Assert.Equal(AuditStatus.InProgress, a.Status);
    }

    [Fact]
    public void Complete_blocked_when_items_unanswered()
    {
        var a = ReadyToPlan();
        a.Plan();
        a.Start();
        a.SendToReview("moving on with one item open for review");
        Assert.Throws<DomainException>(() => a.Complete(End));
    }

    [Fact]
    public void Start_from_draft_is_invalid()
        => Assert.Throws<InvalidStateTransitionException>(() => New().Start());

    [Fact]
    public void Cancel_requires_20_char_reason_and_blocks_when_terminal()
    {
        var a = New();
        Assert.Throws<DomainException>(() => a.Cancel("too short", Now));
        a.Cancel("Engagement deprioritised for the quarter.", Now);
        Assert.Equal(AuditStatus.Cancelled, a.Status);
        Assert.Throws<InvalidStateTransitionException>(() => a.Cancel("Cannot cancel a cancelled audit again.", Now));
    }

    [Fact]
    public void Second_lead_or_auditee_rejected_and_dup_is_idempotent()
    {
        var a = New();
        Assert.Throws<DomainException>(() => a.AddTeamMember(Guid.NewGuid(), TeamRole.Lead, Lead, Now));
        Assert.Throws<DomainException>(() => a.AddTeamMember(Guid.NewGuid(), TeamRole.Auditee, Lead, Now));

        var auditor = Guid.NewGuid();
        a.AddTeamMember(auditor, TeamRole.Auditor, Lead, Now);
        a.AddTeamMember(auditor, TeamRole.Auditor, Lead, Now); // idempotent
        Assert.Equal(1, a.TeamMembers.Count(m => m.UserId == auditor && m.IsActive));
    }

    [Fact]
    public void Cannot_remove_sole_lead_or_auditee()
    {
        var a = New();
        var leadMember = a.TeamMembers.First(m => m.TeamRole == TeamRole.Lead);
        var auditeeMember = a.TeamMembers.First(m => m.TeamRole == TeamRole.Auditee);
        Assert.Throws<DomainException>(() => a.RemoveTeamMember(leadMember.Id, Now));
        Assert.Throws<DomainException>(() => a.RemoveTeamMember(auditeeMember.Id, Now));
    }

    [Fact]
    public void Transfer_lead_swaps_roles_and_blocks_auditee_promotion()
    {
        var a = New();
        var newLead = Guid.NewGuid();
        a.AddTeamMember(newLead, TeamRole.Auditor, Lead, Now);

        // Cannot promote the auditee to lead.
        Assert.Throws<DomainException>(() => a.TransferLead(Auditee, removeOutgoing: false, Now));

        a.TransferLead(newLead, removeOutgoing: false, Now);
        Assert.Equal(newLead, a.LeadUserId);
        Assert.Equal(TeamRole.Lead, a.TeamMembers.First(m => m.UserId == newLead && m.IsActive).TeamRole);
        Assert.Equal(TeamRole.Auditor, a.TeamMembers.First(m => m.UserId == Lead && m.IsActive).TeamRole);
    }

    [Fact]
    public void Checklist_edit_blocked_outside_draft()
    {
        var a = ReadyToPlan();
        var itemId = a.ChecklistItems[0].Id;
        a.Plan();
        Assert.Throws<InvalidStateTransitionException>(() => a.EditChecklistItem(itemId, "x", null, ResponseType.PassFailNa, null, null, true, null));
    }

    [Fact]
    public void Arrange_moves_item_across_sections_and_reorders_while_in_progress()
    {
        var a = ReadyToPlan();
        var i2 = a.AddChecklistItem("Vault reconciled?", null, ResponseType.PassFailNa, "Controls", true, null);
        var i1 = a.ChecklistItems[0]; // ungrouped
        a.AddTeamMember(Guid.NewGuid(), TeamRole.Auditor, Lead, Now);
        a.Plan();
        a.Start(); // InProgress — item edit is blocked here, but arrange (reorder + move) is allowed

        // Move the ungrouped item into "Controls" and put it first.
        a.ArrangeChecklistItems([
            new ChecklistItemPlacement(i1.Id, "Controls"),
            new ChecklistItemPlacement(i2.Id, "Controls"),
        ]);

        Assert.Equal("Controls", a.ChecklistItems.First(i => i.Id == i1.Id).SectionName);
        Assert.Equal(0, a.ChecklistItems.First(i => i.Id == i1.Id).OrderIndex);
        Assert.Equal(1, a.ChecklistItems.First(i => i.Id == i2.Id).OrderIndex);
    }

    [Fact]
    public void Arrange_rejects_a_partial_placement_list()
    {
        var a = ReadyToPlan();
        a.AddChecklistItem("Second", null, ResponseType.PassFailNa, null, true, null);
        var only = a.ChecklistItems[0].Id;
        var ex = Assert.Throws<DomainException>(() => a.ArrangeChecklistItems([new ChecklistItemPlacement(only, null)]));
        Assert.Equal("audit.reorder_mismatch", ex.Code);
    }
}
