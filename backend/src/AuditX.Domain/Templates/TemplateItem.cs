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

    /// <summary>Optional per-type configuration JSON (e.g. choice options, rating scale id, numeric unit/bounds). Opaque to the domain; copied onto the audit's checklist item at audit creation.</summary>
    public string? ResponseConfigJson { get; private set; }

    /// <summary>How severe a failure of this item is, in control-test terms. Copied onto the audit's checklist item and, when set, drives the default severity of any exception raised against it.</summary>
    public ExceptionSeverity? RiskRating { get; private set; }

    internal TemplateItem(
        Guid templateId,
        string prompt,
        string? referenceNotes,
        ResponseType responseType,
        string? sectionName,
        int orderIndex,
        bool isRequired,
        string? defaultAssignmentRuleJson,
        string? responseConfigJson = null,
        ExceptionSeverity? riskRating = null)
    {
        TemplateId = templateId;
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "template.item_prompt_required", "Item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        SectionName = sectionName;
        OrderIndex = orderIndex;
        IsRequired = isRequired;
        DefaultAssignmentRuleJson = defaultAssignmentRuleJson;
        ResponseConfigJson = NormaliseConfig(responseConfigJson);
        RiskRating = riskRating;
    }

    internal void Update(
        string prompt, string? referenceNotes, ResponseType responseType, string? sectionName, bool isRequired,
        string? defaultAssignmentRuleJson, string? responseConfigJson = null, ExceptionSeverity? riskRating = null)
    {
        Prompt = Guard.NotNullOrWhiteSpace(prompt, "template.item_prompt_required", "Item prompt is required.");
        ReferenceNotes = referenceNotes;
        ResponseType = responseType;
        SectionName = sectionName;
        IsRequired = isRequired;
        DefaultAssignmentRuleJson = defaultAssignmentRuleJson;
        ResponseConfigJson = NormaliseConfig(responseConfigJson);
        RiskRating = riskRating;
    }

    private static string? NormaliseConfig(string? json) => string.IsNullOrWhiteSpace(json) ? null : json.Trim();

    internal void SetOrder(int orderIndex) => OrderIndex = orderIndex;

    internal void ClearSection() => SectionName = null;

    internal void RenameSection(string newName) => SectionName = newName;
}
