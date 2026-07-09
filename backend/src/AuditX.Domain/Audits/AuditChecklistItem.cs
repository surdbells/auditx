using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>
/// A checklist item on an audit (M4). Copied from a template version at audit creation; thereafter the
/// audit owns its own copy so template edits never affect a running audit. Execution state is advanced
/// by M5 responses.
/// </summary>
public sealed class AuditChecklistItem : Entity, IBelongsToAggregate
{
    private AuditChecklistItem()
    {
    }

    public Guid AuditId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AuditId;

    public string? SectionName { get; private set; }

    public int OrderIndex { get; private set; }

    public string Prompt { get; private set; } = null!;

    public string? ReferenceNotes { get; private set; }

    public ResponseType ResponseType { get; private set; }

    /// <summary>Optional per-type configuration JSON (e.g. choice options, rating scale, numeric unit/bounds). Opaque to the domain.</summary>
    public string? ResponseConfigJson { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public bool IsRequired { get; private set; }

    public ChecklistItemState ItemState { get; private set; } = ChecklistItemState.NotStarted;

    /// <summary>Item-level flag (PRD Screen 4): set by the M6 hook when an exception is raised for this item.</summary>
    public bool HasException { get; private set; }

    /// <summary>Manager's recorded justification for accepting a Fail without raising an exception (FR-M5-011).</summary>
    public string? FailJustification { get; private set; }

    public Guid? FailJudgedBy { get; private set; }

    public DateTimeOffset? FailJudgedAt { get; private set; }

    internal AuditChecklistItem(
        Guid auditId, string prompt, string? referenceNotes, ResponseType responseType,
        string? sectionName, int orderIndex, bool isRequired, Guid? assignedUserId, string? responseConfigJson = null)
    {
        AuditId = auditId;
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "audit.item_prompt_required", "Checklist item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        ResponseConfigJson = NormaliseConfig(responseConfigJson);
        SectionName = sectionName;
        OrderIndex = orderIndex;
        IsRequired = isRequired;
        AssignedUserId = assignedUserId;
    }

    internal void Update(string prompt, string? referenceNotes, ResponseType responseType, string? responseConfigJson, string? sectionName, bool isRequired, Guid? assignedUserId)
    {
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "audit.item_prompt_required", "Checklist item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        ResponseConfigJson = NormaliseConfig(responseConfigJson);
        SectionName = sectionName;
        IsRequired = isRequired;
        AssignedUserId = assignedUserId;
    }

    private static string? NormaliseConfig(string? json) => string.IsNullOrWhiteSpace(json) ? null : json.Trim();

    internal void SetOrder(int orderIndex) => OrderIndex = orderIndex;

    /// <summary>Cascade a section rename (called from Audit.RenameSection).</summary>
    internal void RenameSection(string newSectionName) => SectionName = newSectionName;

    /// <summary>Move the item to a (possibly null = ungrouped) section — used by drag-drop arrange.</summary>
    internal void SetSection(string? sectionName) => SectionName = sectionName;

    internal void Assign(Guid? userId) => AssignedUserId = userId;

    /// <summary>Set by M5 when a response is recorded/cleared.</summary>
    public void SetState(ChecklistItemState state) => ItemState = state;

    /// <summary>Set/cleared by the M6 exception hook.</summary>
    internal void MarkHasException(bool value) => HasException = value;

    internal void RecordFailJudgement(string justification, Guid judgedBy, DateTimeOffset judgedAt)
    {
        FailJustification = Guard.NotNullOrWhiteSpace(justification, "audit.fail_justification_required", "A justification is required.");
        FailJudgedBy = judgedBy;
        FailJudgedAt = judgedAt;
    }
}
