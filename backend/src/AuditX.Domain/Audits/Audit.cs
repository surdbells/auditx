using AuditX.Domain.Audits.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>
/// An audit engagement (M4). Aggregate root over its team and checklist. Drives the default lifecycle
/// state machine Draft → Planned → In Progress → Under Review → Completed, with cancel/return/reopen
/// edges. Checklist items are copied from a template version at creation, so a running audit is
/// decoupled from later template changes. Configurable per-audit-type workflows are a later increment.
/// </summary>
public sealed class Audit : AggregateRoot
{
    private readonly List<AuditTeamMember> _teamMembers = [];
    private readonly List<AuditChecklistItem> _checklistItems = [];

    private Audit()
    {
    }

    public string Name { get; private set; } = null!;

    public string? ScopeDescription { get; private set; }

    public string AuditType { get; private set; } = null!;

    public AuditStatus Status { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly TargetEndDate { get; private set; }

    public DateOnly? ActualEndDate { get; private set; }

    public Guid? TemplateId { get; private set; }

    public int? TemplateVersion { get; private set; }

    public Guid? PlanItemId { get; private set; }

    public Guid LeadUserId { get; private set; }

    public Guid AuditeeUserId { get; private set; }

    public string ConfigurationVersionsJson { get; private set; } = "{}";

    public string? CancellationReason { get; private set; }

    public string? LastTransitionReason { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<AuditTeamMember> TeamMembers => _teamMembers.AsReadOnly();

    public IReadOnlyList<AuditChecklistItem> ChecklistItems => _checklistItems.AsReadOnly();

    private IEnumerable<AuditTeamMember> ActiveTeam => _teamMembers.Where(m => m.IsActive);

    public static Audit Create(
        string name, string auditType, DateOnly startDate, DateOnly targetEndDate,
        string? scopeDescription, Guid? templateId, int? templateVersion, Guid? planItemId,
        Guid leadUserId, Guid auditeeUserId, string? configurationVersionsJson, Guid? createdBy, DateTimeOffset nowUtc)
    {
        if (targetEndDate < startDate)
        {
            throw new DomainException("audit.target_before_start", "Target end date must be on or after the start date.");
        }

        if (leadUserId == auditeeUserId)
        {
            throw new DomainException("audit.lead_auditee_same_user", "The lead and the auditee must be different users.");
        }

        var audit = new Audit
        {
            Name = Guard.NotNullOrWhiteSpace(name, "audit.name_required", "Audit name is required."),
            AuditType = Guard.NotNullOrWhiteSpace(auditType, "audit.audit_type_required", "Audit type is required."),
            StartDate = startDate,
            TargetEndDate = targetEndDate,
            ScopeDescription = scopeDescription,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            PlanItemId = planItemId,
            LeadUserId = leadUserId,
            AuditeeUserId = auditeeUserId,
            ConfigurationVersionsJson = string.IsNullOrWhiteSpace(configurationVersionsJson) ? "{}" : configurationVersionsJson,
            Status = AuditStatus.Draft,
        };

        audit._teamMembers.Add(new AuditTeamMember(audit.Id, leadUserId, TeamRole.Lead, createdBy, nowUtc));
        audit._teamMembers.Add(new AuditTeamMember(audit.Id, auditeeUserId, TeamRole.Auditee, createdBy, nowUtc));
        audit.RaiseDomainEvent(new AuditCreatedEvent(audit.Id, audit.Name, audit.AuditType, audit.PlanItemId));
        return audit;
    }

    public void UpdateMetadata(string name, string? scopeDescription, DateOnly startDate, DateOnly targetEndDate)
    {
        EnsureStatus("audit.metadata_locked", AuditStatus.Draft, AuditStatus.Planned);
        if (targetEndDate < startDate)
        {
            throw new DomainException("audit.target_before_start", "Target end date must be on or after the start date.");
        }

        Name = Guard.NotNullOrWhiteSpace(name, "audit.name_required", "Audit name is required.");
        ScopeDescription = scopeDescription;
        StartDate = startDate;
        TargetEndDate = targetEndDate;
    }

    // ---- Team management ----

    public AuditTeamMember AddTeamMember(Guid userId, TeamRole role, Guid? addedBy, DateTimeOffset nowUtc)
    {
        EnsureStatus("audit.team_locked", AuditStatus.Draft, AuditStatus.Planned);

        var existing = ActiveTeam.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null)
        {
            return existing; // idempotent
        }

        if (role == TeamRole.Lead && ActiveTeam.Any(m => m.TeamRole == TeamRole.Lead))
        {
            throw new DomainException("audit.lead_exists", "The audit already has a lead; use transfer-lead.");
        }

        if (role == TeamRole.Auditee && ActiveTeam.Any(m => m.TeamRole == TeamRole.Auditee))
        {
            throw new DomainException("audit.auditee_exists", "The audit already has an auditee.");
        }

        var member = new AuditTeamMember(Id, userId, role, addedBy, nowUtc);
        _teamMembers.Add(member);
        RaiseDomainEvent(new AuditTeamMemberAddedEvent(Id, userId, role));
        return member;
    }

    public void RemoveTeamMember(Guid membershipId, DateTimeOffset nowUtc)
    {
        EnsureStatus("audit.team_locked", AuditStatus.Draft, AuditStatus.Planned);
        var member = ActiveTeam.FirstOrDefault(m => m.Id == membershipId)
            ?? throw new DomainException("audit.member_not_found", "Team member not found.");

        if (member.TeamRole == TeamRole.Lead)
        {
            throw new DomainException("audit.cannot_remove_sole_lead", "Transfer the lead before removing them.");
        }

        if (member.TeamRole == TeamRole.Auditee)
        {
            throw new DomainException("audit.cannot_remove_sole_auditee", "The audit must always have an auditee.");
        }

        if (_checklistItems.Any(i => i.AssignedUserId == member.UserId))
        {
            throw new DomainException("audit.reassign_items_first", "Reassign the member's checklist items before removing them.");
        }

        member.Remove(nowUtc);
        RaiseDomainEvent(new AuditTeamMemberRemovedEvent(Id, member.UserId));
    }

    public void TransferLead(Guid newLeadUserId, bool removeOutgoing, DateTimeOffset nowUtc)
    {
        EnsureStatus("audit.team_locked", AuditStatus.Draft, AuditStatus.Planned);
        var incoming = ActiveTeam.FirstOrDefault(m => m.UserId == newLeadUserId)
            ?? throw new DomainException("audit.transfer_target_not_member", "The new lead must already be a team member.");

        if (incoming.TeamRole == TeamRole.Lead)
        {
            throw new DomainException("audit.already_lead", "That member is already the lead.");
        }

        if (incoming.TeamRole == TeamRole.Auditee)
        {
            throw new DomainException("audit.cannot_promote_auditee_to_lead", "The auditee cannot be promoted to lead.");
        }

        var outgoing = ActiveTeam.First(m => m.TeamRole == TeamRole.Lead);
        if (removeOutgoing)
        {
            outgoing.Remove(nowUtc);
        }
        else
        {
            outgoing.ChangeRole(TeamRole.Auditor);
        }

        incoming.ChangeRole(TeamRole.Lead);
        LeadUserId = newLeadUserId;
        RaiseDomainEvent(new AuditLeadTransferredEvent(Id, outgoing.UserId, newLeadUserId));
    }

    // ---- Checklist ----

    public AuditChecklistItem AddChecklistItem(string prompt, string? referenceNotes, ResponseType responseType, string? sectionName, bool isRequired, Guid? assignedUserId)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        var item = new AuditChecklistItem(Id, prompt, referenceNotes, responseType, sectionName, _checklistItems.Count, isRequired, assignedUserId);
        _checklistItems.Add(item);
        return item;
    }

    public void EditChecklistItem(Guid itemId, string prompt, string? referenceNotes, string? sectionName, bool isRequired, Guid? assignedUserId)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft);
        FindItem(itemId).Update(prompt, referenceNotes, sectionName, isRequired, assignedUserId);
    }

    public void RemoveChecklistItem(Guid itemId)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft);
        _checklistItems.Remove(FindItem(itemId));
    }

    // ---- State machine ----

    public void Plan()
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.Draft);
        if (_checklistItems.Count == 0)
        {
            throw new DomainException("audit.checklist_empty", "An audit needs at least one checklist item before planning.");
        }

        if (!ActiveTeam.Any(m => m.TeamRole == TeamRole.Auditor))
        {
            throw new DomainException("audit.no_auditor", "An audit needs at least one auditor before planning.");
        }

        Transition(AuditStatus.Planned, null);
    }

    public void Start() => Transition(AuditStatus.InProgress, null, AuditStatus.Planned);

    public void SendToReview(string? reason)
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.InProgress);
        var unanswered = _checklistItems.Count(i => i.ItemState != ChecklistItemState.Responded);
        if (unanswered > 0 && string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("audit.review_reason_required", "A reason is required when items remain unanswered.");
        }

        LastTransitionReason = reason;
        Transition(AuditStatus.UnderReview, reason);
    }

    public void Complete(DateOnly today)
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.UnderReview);
        if (_checklistItems.Any(i => i.ItemState != ChecklistItemState.Responded))
        {
            throw new DomainException("audit.items_unanswered", "All checklist items must be responded before completion.");
        }

        ActualEndDate = today;
        Status = AuditStatus.Completed;
        RaiseDomainEvent(new AuditTransitionedEvent(Id, AuditStatus.UnderReview, Status, null));
        RaiseDomainEvent(new AuditCompletedEvent(Id, PlanItemId, today));
    }

    public void ReturnToInProgress(string reason)
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.UnderReview);
        LastTransitionReason = Guard.NotNullOrWhiteSpace(reason, "audit.reason_required", "A reason is required.");
        Transition(AuditStatus.InProgress, reason);
    }

    public void Reopen(string reason)
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.Planned);
        LastTransitionReason = Guard.NotNullOrWhiteSpace(reason, "audit.reason_required", "A reason is required.");
        Transition(AuditStatus.Draft, reason);
    }

    public void Cancel(string reason, DateTimeOffset nowUtc)
    {
        if (Status is AuditStatus.Completed or AuditStatus.Cancelled)
        {
            throw new InvalidStateTransitionException("audit.invalid_transition", "A completed or cancelled audit cannot be cancelled.");
        }

        CancellationReason = Guard.MinLength(reason, 20, "audit.cancel_reason_too_short", "A cancellation reason of at least 20 characters is required.");
        Status = AuditStatus.Cancelled;
        RaiseDomainEvent(new AuditCancelledEvent(Id, reason));
    }

    private void Transition(AuditStatus to, string? reason, params AuditStatus[] from)
    {
        if (from.Length > 0 && !from.Contains(Status))
        {
            throw new InvalidStateTransitionException("audit.invalid_transition", $"Cannot move to {to} from {Status}.");
        }

        var fromStatus = Status;
        Status = to;
        RaiseDomainEvent(new AuditTransitionedEvent(Id, fromStatus, to, reason));
    }

    private AuditChecklistItem FindItem(Guid itemId)
        => _checklistItems.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("audit.item_not_found", "Checklist item not found.");

    private void EnsureStatus(string code, params AuditStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStateTransitionException(code, $"Operation not permitted while the audit is {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
