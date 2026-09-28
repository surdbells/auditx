using AuditX.Domain.Common;
using AuditX.Domain.Configuration.Events;

namespace AuditX.Domain.Configuration;

/// <summary>
/// A versioned bank-configuration record (M12). The generic store behind every editable bank knob: per-domain
/// incrementing <see cref="VersionNumber"/>, a domain-specific <see cref="DefinitionJson"/>, a ≥20-char
/// <see cref="ChangeReason"/>, and a single active version per domain (enforced by a filtered unique index on
/// <c>(domain, is_active)</c> WHERE is_active = 1). Mirrors the M7/M8 versioning pattern: creation writes an
/// inactive draft; activation is the policy change worth dual-controlling (maker-checker on activate). Every version
/// is retained (soft-delete) so the timeline and rollbacks stay intact.
/// </summary>
public sealed class InstitutionConfiguration : AggregateRoot, ISoftDeletable
{
    private InstitutionConfiguration()
    {
    }

    /// <summary>The configuration domain (one of <see cref="ConfigurationDomains"/>), e.g. <c>exception_defaults</c>.</summary>
    public string Domain { get; private set; } = null!;

    /// <summary>Per-domain incrementing version number (1-based).</summary>
    public int VersionNumber { get; private set; }

    /// <summary>The domain-specific definition payload (NVARCHAR(MAX) JSON), validated per-domain by the application layer.</summary>
    public string DefinitionJson { get; private set; } = null!;

    public bool IsActive { get; private set; }

    /// <summary>A ≥20-char reason captured on draft (and re-captured on activate/rollback). Required, never null.</summary>
    public string ChangeReason { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? ActivatedBy { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] Version { get; private set; } = [];

    /// <summary>Draft a new (inactive) version for a domain. A change reason of ≥20 chars is required (G1).</summary>
    public static InstitutionConfiguration CreateDraft(
        string domain, int versionNumber, string definitionJson, string changeReason, Guid createdBy, DateTimeOffset nowUtc)
    {
        var config = new InstitutionConfiguration
        {
            Domain = Guard.NotNullOrWhiteSpace(domain, "configuration.domain_required", "A configuration domain is required."),
            VersionNumber = versionNumber,
            DefinitionJson = Guard.NotNullOrWhiteSpace(definitionJson, "configuration.definition_required", "A configuration definition is required."),
            ChangeReason = Guard.MinLength(changeReason, 20, "configuration.change_reason_required", "A change reason of at least 20 characters is required."),
            IsActive = false,
            CreatedByUserId = createdBy,
            CreatedAtUtc = nowUtc,
        };
        config.RaiseDomainEvent(new ConfigurationVersionCreatedEvent(config.Id, config.Domain, versionNumber, createdBy));
        return config;
    }

    /// <summary>Activate this version. The prior active version for the domain is deactivated by the caller (atomic switch).</summary>
    public void Activate(Guid activatedBy, DateTimeOffset nowUtc)
    {
        IsActive = true;
        ActivatedBy = activatedBy;
        ActivatedAt = nowUtc;
        RaiseDomainEvent(new ConfigurationVersionActivatedEvent(Id, Domain, VersionNumber, activatedBy));
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
