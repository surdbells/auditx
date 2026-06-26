using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Templates;

/// <summary>
/// An audit-checklist template (M2). Encodes the audit function's methodology as a reusable, versioned
/// checklist. Items and sections are editable only while the template is in <see cref="TemplateStatus.Draft"/>;
/// publishing snapshots the working set into an immutable <see cref="TemplateVersion"/>. Audits copy a
/// template's items at creation time, so templates may evolve without affecting running audits.
/// </summary>
public sealed class Template : AggregateRoot, ISoftDeletable
{
    private readonly List<TemplateItem> _items = [];
    private readonly List<TemplateSection> _sections = [];
    private readonly List<TemplateVersion> _versions = [];

    private Template()
    {
    }

    public string Name { get; private set; } = null!;

    public string AuditType { get; private set; } = null!;

    public string Description { get; private set; } = string.Empty;

    public TemplateStatus Status { get; private set; }

    public int CurrentVersion { get; private set; }

    public Guid? ClonedFromTemplateId { get; private set; }

    public IReadOnlyList<TemplateItem> Items => _items.AsReadOnly();

    public IReadOnlyList<TemplateSection> Sections => _sections.AsReadOnly();

    public IReadOnlyList<TemplateVersion> Versions => _versions.AsReadOnly();

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static Template CreateDraft(string name, string auditType, string? description, Guid? clonedFromTemplateId = null)
    {
        return new Template
        {
            Name = Guard.NotNullOrWhiteSpace(name, "template.name_required", "Template name is required."),
            AuditType = Guard.NotNullOrWhiteSpace(auditType, "template.audit_type_required", "Audit type is required."),
            Description = description?.Trim() ?? string.Empty,
            Status = TemplateStatus.Draft,
            CurrentVersion = 1,
            ClonedFromTemplateId = clonedFromTemplateId,
        };
    }

    public void UpdateMetadata(string name, string? description)
    {
        EnsureDraft();
        Name = Guard.NotNullOrWhiteSpace(name, "template.name_required", "Template name is required.");
        Description = description?.Trim() ?? string.Empty;
    }

    public TemplateItem AddItem(string prompt, string? referenceNotes, ResponseType responseType, string? sectionName, bool isRequired, string? defaultAssignmentRuleJson)
    {
        EnsureDraft();
        EnsureSectionExists(sectionName);
        var item = new TemplateItem(Id, prompt, referenceNotes, responseType, sectionName, _items.Count, isRequired, defaultAssignmentRuleJson);
        _items.Add(item);
        return item;
    }

    public void UpdateItem(Guid itemId, string prompt, string? referenceNotes, ResponseType responseType, string? sectionName, bool isRequired, string? defaultAssignmentRuleJson)
    {
        EnsureDraft();
        EnsureSectionExists(sectionName);
        FindItem(itemId).Update(prompt, referenceNotes, responseType, sectionName, isRequired, defaultAssignmentRuleJson);
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureDraft();
        _items.Remove(FindItem(itemId));
        Renumber();
    }

    public void ReorderItems(IReadOnlyList<Guid> orderedItemIds)
    {
        EnsureDraft();
        if (orderedItemIds.Count != _items.Count || orderedItemIds.Distinct().Count() != _items.Count)
        {
            throw new DomainException("template.reorder_mismatch", "The reorder must list every item exactly once.");
        }

        for (var index = 0; index < orderedItemIds.Count; index++)
        {
            FindItem(orderedItemIds[index]).SetOrder(index);
        }
    }

    public TemplateSection AddSection(string name)
    {
        EnsureDraft();
        if (_sections.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("template.section_exists", $"A section named '{name}' already exists.");
        }

        var section = new TemplateSection(Id, name, _sections.Count);
        _sections.Add(section);
        return section;
    }

    public void RenameSection(string currentName, string newName)
    {
        EnsureDraft();
        var section = FindSection(currentName);
        if (!string.Equals(currentName, newName, StringComparison.OrdinalIgnoreCase)
            && _sections.Any(s => string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("template.section_exists", $"A section named '{newName}' already exists.");
        }

        section.Rename(newName);
        foreach (var item in _items.Where(i => string.Equals(i.SectionName, currentName, StringComparison.OrdinalIgnoreCase)))
        {
            item.RenameSection(newName);
        }
    }

    public void RemoveSection(string name)
    {
        EnsureDraft();
        var section = FindSection(name);
        if (_items.Any(i => string.Equals(i.SectionName, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("template.section_in_use", "Cannot remove a section that still has items.");
        }

        _sections.Remove(section);
    }

    /// <summary>Publish the working set as a new immutable version. Requires Draft and at least one item.</summary>
    public TemplateVersion Publish(DateTimeOffset publishedAtUtc, Func<IReadOnlyList<TemplateItemSnapshot>, string> serializeSnapshot)
    {
        EnsureDraft();
        if (_items.Count == 0)
        {
            throw new DomainException("template.empty", "A template must have at least one item before it can be published.");
        }

        var snapshot = _items
            .OrderBy(i => i.OrderIndex)
            .Select(i => new TemplateItemSnapshot(i.Prompt, i.ReferenceNotes, i.ResponseType, i.SectionName, i.OrderIndex, i.IsRequired, i.DefaultAssignmentRuleJson))
            .ToArray();

        var version = new TemplateVersion(Id, CurrentVersion, publishedAtUtc, serializeSnapshot(snapshot));
        _versions.Add(version);
        Status = TemplateStatus.Published;
        return version;
    }

    /// <summary>Open a new draft based on the published template; the working items remain editable (US-M2-009).</summary>
    public void CreateNewDraft()
    {
        if (Status != TemplateStatus.Published)
        {
            throw new InvalidStateTransitionException("template.not_published", "Only a published template can start a new draft.");
        }

        CurrentVersion++;
        Status = TemplateStatus.Draft;
    }

    public void Archive()
    {
        if (Status == TemplateStatus.Archived)
        {
            throw new InvalidStateTransitionException("template.already_archived", "Template is already archived.");
        }

        Status = TemplateStatus.Archived;
    }

    public void Unarchive()
    {
        if (Status != TemplateStatus.Archived)
        {
            throw new InvalidStateTransitionException("template.not_archived", "Only an archived template can be unarchived.");
        }

        Status = _versions.Count > 0 ? TemplateStatus.Published : TemplateStatus.Draft;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private void EnsureDraft()
    {
        if (Status != TemplateStatus.Draft)
        {
            throw new InvalidStateTransitionException("template.not_draft", "Only a draft template can be edited; create a new draft version first.");
        }
    }

    private void EnsureSectionExists(string? sectionName)
    {
        if (!string.IsNullOrWhiteSpace(sectionName) && !_sections.Any(s => string.Equals(s.Name, sectionName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("template.unknown_section", $"Section '{sectionName}' does not exist on this template.");
        }
    }

    private TemplateItem FindItem(Guid itemId)
        => _items.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("template.item_not_found", "Checklist item not found.");

    private TemplateSection FindSection(string name)
        => _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new DomainException("template.section_not_found", $"Section '{name}' not found.");

    private void Renumber()
    {
        var ordered = _items.OrderBy(i => i.OrderIndex).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SetOrder(index);
        }
    }
}
