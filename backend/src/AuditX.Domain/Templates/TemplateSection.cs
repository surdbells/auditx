using AuditX.Domain.Common;

namespace AuditX.Domain.Templates;

/// <summary>A named grouping of checklist items within a template (e.g. "Cash Handling").</summary>
public sealed class TemplateSection : Entity
{
    private TemplateSection()
    {
    }

    public Guid TemplateId { get; private set; }

    public string Name { get; private set; } = null!;

    public int OrderIndex { get; private set; }

    internal TemplateSection(Guid templateId, string name, int orderIndex)
    {
        TemplateId = templateId;
        Name = Guard.NotNullOrWhiteSpace(name, "template.section_name_required", "Section name is required.");
        OrderIndex = orderIndex;
    }

    internal void Rename(string name) => Name = Guard.NotNullOrWhiteSpace(name, "template.section_name_required", "Section name is required.");
}
