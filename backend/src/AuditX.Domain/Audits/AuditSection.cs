using AuditX.Domain.Common;

namespace AuditX.Domain.Audits;

/// <summary>
/// A named grouping of checklist items within an audit (e.g. "Cash Handling"). Mirrors
/// <c>TemplateSection</c>: items reference a section by its (case-insensitive) <see cref="Name"/>, while the
/// entity supplies first-class ordering, rename/remove operations, and duplicate-name protection.
/// A child of the <see cref="Audit"/> aggregate.
/// </summary>
public sealed class AuditSection : Entity, IBelongsToAggregate
{
    private AuditSection()
    {
    }

    public Guid AuditId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AuditId;

    public string Name { get; private set; } = null!;

    public int OrderIndex { get; private set; }

    internal AuditSection(Guid auditId, string name, int orderIndex)
    {
        AuditId = auditId;
        Name = Guard.NotNullOrWhiteSpace(name, "audit.section_name_required", "Section name is required.");
        OrderIndex = orderIndex;
    }

    internal void Rename(string name) => Name = Guard.NotNullOrWhiteSpace(name, "audit.section_name_required", "Section name is required.");

    internal void SetOrder(int orderIndex) => OrderIndex = orderIndex;
}
