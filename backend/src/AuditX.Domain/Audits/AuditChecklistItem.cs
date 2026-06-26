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

    public Guid? AssignedUserId { get; private set; }

    public bool IsRequired { get; private set; }

    public ChecklistItemState ItemState { get; private set; } = ChecklistItemState.NotStarted;

    internal AuditChecklistItem(
        Guid auditId, string prompt, string? referenceNotes, ResponseType responseType,
        string? sectionName, int orderIndex, bool isRequired, Guid? assignedUserId)
    {
        AuditId = auditId;
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "audit.item_prompt_required", "Checklist item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        SectionName = sectionName;
        OrderIndex = orderIndex;
        IsRequired = isRequired;
        AssignedUserId = assignedUserId;
    }

    internal void Update(string prompt, string? referenceNotes, string? sectionName, bool isRequired, Guid? assignedUserId)
    {
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "audit.item_prompt_required", "Checklist item prompt is required.");
        ReferenceNotes = referenceNotes;
        SectionName = sectionName;
        IsRequired = isRequired;
        AssignedUserId = assignedUserId;
    }

    internal void SetOrder(int orderIndex) => OrderIndex = orderIndex;

    internal void Assign(Guid? userId) => AssignedUserId = userId;

    /// <summary>Set by M5 when a response is recorded/cleared.</summary>
    public void SetState(ChecklistItemState state) => ItemState = state;
}
