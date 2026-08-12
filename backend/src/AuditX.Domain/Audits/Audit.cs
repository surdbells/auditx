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
    private readonly List<AuditSection> _sections = [];
    private readonly List<AuditChecklistItem> _checklistItems = [];
    private readonly List<ChecklistResponse> _responses = [];

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

    /// <summary>The audit-universe entity this engagement covers (direct link, so ad-hoc audits aren't entity-less).</summary>
    public Guid? AuditableEntityId { get; private set; }

    public Guid LeadUserId { get; private set; }

    public Guid AuditeeUserId { get; private set; }

    /// <summary>
    /// True for a self-assessment: the area owner assesses their own area, so the lead and auditee are the same
    /// person and no independent auditor is required. Reuses the whole checklist/response/exception engine.
    /// </summary>
    public bool IsSelfAssessment { get; private set; }

    public string ConfigurationVersionsJson { get; private set; } = "{}";

    public string? CancellationReason { get; private set; }

    public string? LastTransitionReason { get; private set; }

    /// <summary>Planned effort in hours — the budget baseline for budget-vs-actual (P0-B). Null when unset.</summary>
    public decimal? BudgetedHours { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<AuditTeamMember> TeamMembers => _teamMembers.AsReadOnly();

    public IReadOnlyList<AuditSection> Sections => _sections.AsReadOnly();

    public IReadOnlyList<AuditChecklistItem> ChecklistItems => _checklistItems.AsReadOnly();

    public IReadOnlyList<ChecklistResponse> Responses => _responses.AsReadOnly();

    private IEnumerable<AuditTeamMember> ActiveTeam => _teamMembers.Where(m => m.IsActive);

    public static Audit Create(
        string name, string auditType, DateOnly startDate, DateOnly targetEndDate,
        string? scopeDescription, Guid? templateId, int? templateVersion, Guid? planItemId, Guid? auditableEntityId,
        Guid leadUserId, Guid auditeeUserId, string? configurationVersionsJson, Guid? createdBy, DateTimeOffset nowUtc,
        bool isSelfAssessment = false)
    {
        if (targetEndDate < startDate)
        {
            throw new DomainException("audit.target_before_start", "Target end date must be on or after the start date.");
        }

        // Independence: a normal audit needs a separate lead and auditee. A self-assessment is the exception —
        // the area owner assesses their own area, so the two are deliberately the same person.
        if (leadUserId == auditeeUserId && !isSelfAssessment)
        {
            throw new DomainException("audit.lead_auditee_same_user", "The lead and the auditee must be different users.");
        }

        if (isSelfAssessment && leadUserId != auditeeUserId)
        {
            throw new DomainException("audit.self_assessment_single_user", "A self-assessment's assessor is both the lead and the auditee.");
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
            AuditableEntityId = auditableEntityId,
            LeadUserId = leadUserId,
            AuditeeUserId = auditeeUserId,
            IsSelfAssessment = isSelfAssessment,
            ConfigurationVersionsJson = string.IsNullOrWhiteSpace(configurationVersionsJson) ? "{}" : configurationVersionsJson,
            Status = AuditStatus.Draft,
        };

        // For a self-assessment the one person is the assessor — add them once (as Lead) rather than duplicating
        // the same user under two team roles.
        audit._teamMembers.Add(new AuditTeamMember(audit.Id, leadUserId, TeamRole.Lead, createdBy, nowUtc));
        if (!isSelfAssessment)
        {
            audit._teamMembers.Add(new AuditTeamMember(audit.Id, auditeeUserId, TeamRole.Auditee, createdBy, nowUtc));
        }

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

    /// <summary>Sets (or clears, with null) the planned effort budget in hours. Allowed at any status.</summary>
    public void SetBudgetedHours(decimal? hours)
    {
        if (hours is { } h)
        {
            Guard.Against(h < 0, "audit.budget_negative", "Budgeted hours cannot be negative.");
            Guard.Against(h > 1_000_000, "audit.budget_too_large", "Budgeted hours is implausibly large.");
        }

        BudgetedHours = hours;
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

    // ---- Sections ----

    /// <summary>Add a named section. Mirrors the template section CRUD; allowed while the checklist is editable.</summary>
    public AuditSection AddSection(string name)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        var trimmed = Guard.NotNullOrWhiteSpace(name, "audit.section_name_required", "Section name is required.").Trim();
        if (_sections.Any(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("audit.section_exists", $"A section named '{trimmed}' already exists.");
        }

        var section = new AuditSection(Id, trimmed, _sections.Count);
        _sections.Add(section);
        return section;
    }

    /// <summary>Rename a section and cascade the new name to every item that referenced it.</summary>
    public void RenameSection(string currentName, string newName)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        var trimmed = Guard.NotNullOrWhiteSpace(newName, "audit.section_name_required", "Section name is required.").Trim();
        var section = FindSection(currentName);
        if (!string.Equals(currentName, trimmed, StringComparison.OrdinalIgnoreCase)
            && _sections.Any(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("audit.section_exists", $"A section named '{trimmed}' already exists.");
        }

        section.Rename(trimmed);
        foreach (var item in _checklistItems.Where(i => string.Equals(i.SectionName, currentName, StringComparison.OrdinalIgnoreCase)))
        {
            item.RenameSection(trimmed);
        }
    }

    /// <summary>Remove an empty section. Reassign its items first (mirrors the template rule).</summary>
    public void RemoveSection(string name)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        var section = FindSection(name);
        if (_checklistItems.Any(i => string.Equals(i.SectionName, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("audit.section_in_use", "Cannot remove a section that still has items.");
        }

        _sections.Remove(section);
    }

    public void ReorderSections(IReadOnlyList<string> orderedSectionNames)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        var distinct = orderedSectionNames.Select(n => n.ToLowerInvariant()).Distinct().Count();
        if (orderedSectionNames.Count != _sections.Count || distinct != _sections.Count)
        {
            throw new DomainException("audit.reorder_mismatch", "The reorder must list every section exactly once.");
        }

        for (var index = 0; index < orderedSectionNames.Count; index++)
        {
            FindSection(orderedSectionNames[index]).SetOrder(index);
        }
    }

    // ---- Checklist ----

    public AuditChecklistItem AddChecklistItem(
        string prompt, string? referenceNotes, ResponseType responseType, string? sectionName, bool isRequired,
        Guid? assignedUserId, string? responseConfigJson = null, ExceptionSeverity? riskRating = null, Guid? controlId = null)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        var section = EnsureSection(sectionName);
        var item = new AuditChecklistItem(Id, prompt, referenceNotes, responseType, section, _checklistItems.Count, isRequired, assignedUserId, responseConfigJson, riskRating, controlId);
        _checklistItems.Add(item);
        return item;
    }

    public void EditChecklistItem(
        Guid itemId, string prompt, string? referenceNotes, ResponseType responseType, string? responseConfigJson,
        string? sectionName, bool isRequired, Guid? assignedUserId, ExceptionSeverity? riskRating = null, Guid? controlId = null)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft);
        var section = EnsureSection(sectionName);
        FindItem(itemId).Update(prompt, referenceNotes, responseType, responseConfigJson, section, isRequired, assignedUserId, riskRating, controlId);
    }

    public void RemoveChecklistItem(Guid itemId)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft);
        _checklistItems.Remove(FindItem(itemId));
    }

    /// <summary>Re-stamp checklist item order from a full ordered id list (drag-and-drop reordering).</summary>
    public void ReorderChecklistItems(IReadOnlyList<Guid> orderedItemIds)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        if (orderedItemIds.Count != _checklistItems.Count
            || !orderedItemIds.ToHashSet().SetEquals(_checklistItems.Select(i => i.Id)))
        {
            throw new DomainException("audit.reorder_mismatch", "The reorder must list exactly the current checklist items once each.");
        }

        for (var index = 0; index < orderedItemIds.Count; index++)
        {
            FindItem(orderedItemIds[index]).SetOrder(index);
        }
    }

    /// <summary>
    /// Drag-and-drop arrange: set each item's section and order from a full ordered placement list. Supports
    /// both within-section reordering and cross-section moves in one operation (allowed while the checklist is
    /// editable, i.e. Draft or In Progress). Sections are auto-created if a move targets a not-yet-existing name.
    /// </summary>
    public void ArrangeChecklistItems(IReadOnlyList<ChecklistItemPlacement> placements)
    {
        EnsureStatus("audit.checklist_locked", AuditStatus.Draft, AuditStatus.InProgress);
        if (placements.Count != _checklistItems.Count
            || !placements.Select(p => p.ItemId).ToHashSet().SetEquals(_checklistItems.Select(i => i.Id)))
        {
            throw new DomainException("audit.reorder_mismatch", "The arrange must list exactly the current checklist items once each.");
        }

        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            var item = FindItem(placement.ItemId);
            item.SetSection(EnsureSection(placement.SectionName));
            item.SetOrder(index);
        }
    }

    // ---- State machine ----

    public void Plan()
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.Draft);
        if (_checklistItems.Count == 0)
        {
            throw new DomainException("audit.checklist_empty", "An audit needs at least one checklist item before planning.");
        }

        // A self-assessment has no independent auditor — the assessor (lead) does the work themselves — so the
        // "needs an auditor" gate applies only to normal audits.
        if (!IsSelfAssessment && !ActiveTeam.Any(m => m.TeamRole == TeamRole.Auditor))
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

    // ---- Execution / fieldwork (M5) ----

    /// <summary>
    /// Create-or-update the response to a checklist item (US-M5-001/002/003). Flips the item state and,
    /// when the last item is finalised, auto-transitions the audit to Under Review (US-M4-009) in the same
    /// aggregate mutation. Authorisation (assignee / manager override) is enforced in the application layer.
    /// </summary>
    public ResponseMutation RecordResponse(Guid itemId, ResponseVerdict? verdict, string? comment, bool isDraft, Guid actorUserId, bool requireCommentOnPass, DateTimeOffset nowUtc, string? valueJson = null)
    {
        EnsureStatus("audit.responses_locked", AuditStatus.InProgress);
        var item = FindItem(itemId);

        var response = _responses.FirstOrDefault(r => r.ChecklistItemId == itemId);
        var before = response?.ToState();
        if (response is null)
        {
            response = new ChecklistResponse(Id, itemId, actorUserId);
            _responses.Add(response);
        }

        response.Apply(verdict, comment, valueJson, isDraft, actorUserId, requireCommentOnPass, item.ResponseType.IsValueType(), nowUtc);
        item.SetState(isDraft ? ChecklistItemState.InProgress : ChecklistItemState.Responded);
        RaiseDomainEvent(new ItemRespondedEvent(Id, itemId, response.Id, verdict, isDraft));

        var autoTransitioned = false;
        if (!isDraft && _checklistItems.All(i => i.ItemState == ChecklistItemState.Responded))
        {
            SendToReview(reason: null);
            autoTransitioned = true;
        }

        return new ResponseMutation(response, before, response.ToState(), autoTransitioned);
    }

    /// <summary>Discard a draft response (US-M5-003); the item returns to Not Started. Author/manager check is in the app layer.</summary>
    public Guid DiscardDraft(Guid itemId)
    {
        EnsureStatus("audit.responses_locked", AuditStatus.InProgress);
        var response = _responses.FirstOrDefault(r => r.ChecklistItemId == itemId)
            ?? throw new DomainException("response.not_found", "There is no response to discard.");
        if (!response.IsDraft)
        {
            throw new DomainException("response.not_draft", "Only a draft response may be discarded.");
        }

        var responseId = response.Id;
        _responses.Remove(response);
        FindItem(itemId).SetState(ChecklistItemState.NotStarted);
        RaiseDomainEvent(new DraftDiscardedEvent(Id, itemId, responseId));
        return responseId;
    }

    /// <summary>Assign (or clear) a checklist item's owner (US-M5-012). Manager-only is enforced in the app layer.</summary>
    public void AssignItem(Guid itemId, Guid? assigneeUserId)
    {
        EnsureStatus("audit.assignment_locked", AuditStatus.InProgress, AuditStatus.UnderReview);
        var item = FindItem(itemId);
        if (assigneeUserId is { } uid && !ActiveTeam.Any(m => m.UserId == uid))
        {
            throw new DomainException("audit.assignee_not_team_member", "An item can only be assigned to an active team member.");
        }

        item.Assign(assigneeUserId);
        RaiseDomainEvent(new ItemAssignedEvent(Id, itemId, assigneeUserId));
    }

    /// <summary>Record a manager's judgement that a failed item does not warrant an exception (FR-M5-011).</summary>
    public void RecordFailJudgement(Guid itemId, string justification, Guid judgedBy, DateTimeOffset nowUtc)
    {
        EnsureStatus("audit.invalid_transition", AuditStatus.InProgress, AuditStatus.UnderReview);
        var item = FindItem(itemId);
        var hasFail = _responses.Any(r => r.ChecklistItemId == itemId && r.Verdict == ResponseVerdict.Fail && !r.IsDraft);
        if (!hasFail)
        {
            throw new DomainException("audit.no_fail_response", "Only an item with a Fail response can carry a fail judgement.");
        }

        item.RecordFailJudgement(justification, judgedBy, nowUtc);
    }

    /// <summary>Set/clear the item-level exception flag (M6 hook).</summary>
    public void SetItemException(Guid itemId, bool hasException) => FindItem(itemId).MarkHasException(hasException);

    /// <summary>
    /// Apply a post-response score computed by the application layer (it alone can resolve a RatingScale) to
    /// the response just recorded by <see cref="RecordResponse"/>.
    /// </summary>
    public void SetResponseScore(Guid itemId, decimal? score)
    {
        var response = _responses.FirstOrDefault(r => r.ChecklistItemId == itemId)
            ?? throw new DomainException("response.not_found", "There is no response to score.");
        response.SetScore(score);
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

    private AuditSection FindSection(string name)
        => _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new DomainException("audit.section_not_found", $"Section '{name}' not found.");

    /// <summary>
    /// Resolve an item's section: null/blank means ungrouped; otherwise find the section (case-insensitively)
    /// and return its canonical name, auto-creating it if it does not yet exist. Auto-create keeps free-text
    /// section entry working and back-fills a first-class section for items imported from a template.
    /// </summary>
    private string? EnsureSection(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return null;
        }

        var trimmed = sectionName.Trim();
        var existing = _sections.FirstOrDefault(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing.Name;
        }

        _sections.Add(new AuditSection(Id, trimmed, _sections.Count));
        return trimmed;
    }

    private void EnsureStatus(string code, params AuditStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStateTransitionException(code, $"Operation not permitted while the audit is {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
