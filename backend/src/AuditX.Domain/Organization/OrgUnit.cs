using AuditX.Domain.Common;

namespace AuditX.Domain.Organization;

/// <summary>
/// A node in the bank's organisational hierarchy (division → department → branch/unit). The roll-up dimension
/// that auditable entities, users, audits and findings report against ("by business unit"). Forms an acyclic
/// tree; the acyclicity check runs in the application layer (which holds the graph), mirroring the audit universe.
/// </summary>
public sealed class OrgUnit : Entity
{
    private OrgUnit()
    {
    }

    public string Name { get; private set; } = null!;

    /// <summary>Short, unique, upper-cased code (e.g. <c>RETAIL</c>).</summary>
    public string Code { get; private set; } = null!;

    public Guid? ParentOrgUnitId { get; private set; }

    public bool IsArchived { get; private set; }

    /// <summary>Optimistic-concurrency token (rowversion).</summary>
    public byte[] Version { get; private set; } = [];

    public static OrgUnit Create(string name, string code, Guid? parentOrgUnitId) => new()
    {
        Name = Guard.NotNullOrWhiteSpace(name, "org_unit.name_required", "An org-unit name is required."),
        Code = Guard.NotNullOrWhiteSpace(code, "org_unit.code_required", "An org-unit code is required.").Trim().ToUpperInvariant(),
        ParentOrgUnitId = parentOrgUnitId,
    };

    public void Rename(string name) =>
        Name = Guard.NotNullOrWhiteSpace(name, "org_unit.name_required", "An org-unit name is required.");

    /// <summary>Re-parents the unit. Acyclicity is enforced by the caller (application layer holds the graph).</summary>
    public void SetParent(Guid? parentOrgUnitId)
    {
        Guard.Against(parentOrgUnitId == Id, "org_unit.self_parent", "An org unit cannot be its own parent.");
        ParentOrgUnitId = parentOrgUnitId;
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;
}
