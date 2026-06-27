using AuditX.Domain.Common;
using AuditX.Domain.Reports.Events;

namespace AuditX.Domain.Reports;

/// <summary>
/// A versioned, bank-configurable report template (M8). Mirrors <c>SanctionsGridVersion</c>: creation writes an
/// inactive draft; activation is the policy change worth a ≥20-char reason. Exactly one template is active
/// bank-wide (filtered unique index on <c>is_active = 1</c>); the prior active row is deactivated by the handler
/// using the set-based DeactivateActiveAsync-before-activate switch (the M7 grid HIGH-fix lesson). Every version
/// is retained (version_number unique, soft-delete) so historical reports can render against their snapshot.
/// </summary>
public sealed class ReportTemplate : AggregateRoot, ISoftDeletable
{
    private ReportTemplate()
    {
    }

    public string Name { get; private set; } = null!;

    public int VersionNumber { get; private set; }

    /// <summary>JSON: cover / sections[] / branding / conditions. Snapshotted onto each Report at generation.</summary>
    public string TemplateDefinitionJson { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public string? ActivationReason { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? ActivatedBy { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static ReportTemplate CreateVersion(string name, string definitionJson, int versionNumber, Guid createdBy, DateTimeOffset nowUtc)
    {
        var template = new ReportTemplate
        {
            Name = Guard.NotNullOrWhiteSpace(name, "report_template.name_required", "A template name is required."),
            VersionNumber = versionNumber,
            TemplateDefinitionJson = Guard.NotNullOrWhiteSpace(definitionJson, "report_template.definition_required", "A template definition is required."),
            IsActive = false,
            CreatedByUserId = createdBy,
            CreatedAtUtc = nowUtc,
        };
        template.RaiseDomainEvent(new ReportTemplateCreatedEvent(template.Id, versionNumber, createdBy));
        return template;
    }

    /// <summary>Activate this version (reason ≥ 20). The prior active version is deactivated by the caller.</summary>
    public void Activate(string activationReason, Guid activatedBy, DateTimeOffset nowUtc)
    {
        ActivationReason = Guard.MinLength(activationReason, 20, "report_template.reason_required", "An activation reason of at least 20 characters is required.");
        IsActive = true;
        ActivatedBy = activatedBy;
        ActivatedAt = nowUtc;
        RaiseDomainEvent(new ReportTemplateActivatedEvent(Id, VersionNumber, activatedBy));
    }

    /// <summary>Deactivate the previously-active version during an atomic switch (keeps the single-active invariant).</summary>
    public void Deactivate() => IsActive = false;

    void ISoftDeletable.SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        DeletedBy = deletedBy;
    }
}
