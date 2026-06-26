using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Templates;

/// <summary>
/// A single checklist item in a template's working (draft) set. Editable only while the owning template
/// is in Draft; when the template is published the items are snapshotted into a <see cref="TemplateVersion"/>.
/// </summary>
public sealed class TemplateItem : Entity
{
    private TemplateItem()
    {
    }

    public Guid TemplateId { get; private set; }

    public string Prompt { get; private set; } = null!;

    public string? ReferenceNotes { get; private set; }

    public ResponseType ResponseType { get; private set; }

    /// <summary>Optional grouping; references a <see cref="TemplateSection.Name"/> on the same template.</summary>
    public string? SectionName { get; private set; }

    public int OrderIndex { get; private set; }

    public bool IsRequired { get; private set; }

    /// <summary>Optional JSON describing how this item should be assigned when an audit is created (US-M2-007).</summary>
    public string? DefaultAssignmentRuleJson { get; private set; }

    internal TemplateItem(
        Guid templateId,
        string prompt,
        string? referenceNotes,
        ResponseType responseType,
        string? sectionName,
        int orderIndex,
        bool isRequired,
        string? defaultAssignmentRuleJson)
    {
        TemplateId = templateId;
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "template.item_prompt_required", "Item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        SectionName = sectionName;
        OrderIndex = orderIndex;
        IsRequired = isRequired;
        DefaultAssignmentRuleJson = defaultAssignmentRuleJson;
    }

    internal void Update(string prompt, string? referenceNotes, ResponseType responseType, string? sectionName, bool isRequired, string? defaultAssignmentRuleJson)
    {
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "template.item_prompt_required", "Item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        SectionName = sectionName;
        IsRequired = isRequired;
        DefaultAssignmentRuleJson = defaultAssignmentRuleJson;
    }

    internal void SetOrder(int orderIndex) => OrderIndex = orderIndex;

    internal void ClearSection() => SectionName = null;

    internal void RenameSection(string newName) => SectionName = newName;
}
