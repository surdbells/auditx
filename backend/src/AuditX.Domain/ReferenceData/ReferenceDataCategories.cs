namespace AuditX.Domain.ReferenceData;

/// <summary>
/// The catalogue of managed reference-data lists carried by the generic store. Each category is a small,
/// user-managed list surfaced as a dropdown (replacing today's free text). New lists drop in here without a
/// schema change; the admin UI reads <see cref="All"/> to render the list of manageable categories.
/// </summary>
public static class ReferenceDataCategories
{
    /// <summary>Audit types (financial statement, branch operations, …) used by templates, plan items and audits.</summary>
    public const string AuditType = "audit_type";

    /// <summary>Exception categories (cash handling, credit, AML/KYC, …) used when raising exceptions.</summary>
    public const string ExceptionCategory = "exception_category";

    /// <summary>Root-cause categories (process gap, human error, system limitation, …) — the finding root-cause taxonomy (P2-A).</summary>
    public const string RootCauseCategory = "root_cause_category";

    /// <summary>Non-conformance categories (policy breach, regulatory breach, control failure, …) — the finding non-conformance taxonomy for compliance reporting.</summary>
    public const string NonConformanceCategory = "non_conformance_category";

    /// <summary>Evidence document types (policy, screenshot, report, contract, …) — categorises requested evidence (P2-D).</summary>
    public const string EvidenceDocumentType = "evidence_document_type";

    /// <summary>Auditable-entity types (branch, process, system, vendor, …) used across the audit universe.</summary>
    public const string EntityType = "entity_type";

    /// <summary>Sanctions-grid categories (cash handling, process breach, …) used to key grid cells (M7).</summary>
    public const string SanctionCategory = "sanction_category";

    /// <summary>Risk categories (operational, financial, compliance, …) used by the risk register.</summary>
    public const string RiskCategory = "risk_category";

    /// <summary>Regulatory authorities (central bank, securities commission, …) that issue the regulations tracked in the compliance register.</summary>
    public const string RegulationAuthority = "regulation_authority";

    /// <summary>Regulation categories (prudential, AML/CFT, consumer protection, …) used to classify regulations in the compliance register.</summary>
    public const string RegulationCategory = "regulation_category";

    /// <summary>Every category the store recognises, with its human label — the admin UI's manageable-list menu.</summary>
    public static readonly IReadOnlyList<ReferenceDataCategoryDescriptor> All =
    [
        new(AuditType, "Audit types"),
        new(ExceptionCategory, "Exception categories"),
        new(RootCauseCategory, "Root-cause categories"),
        new(NonConformanceCategory, "Non-conformance categories"),
        new(EvidenceDocumentType, "Evidence document types"),
        new(EntityType, "Entity types"),
        new(SanctionCategory, "Sanction categories"),
        new(RiskCategory, "Risk categories"),
        new(RegulationAuthority, "Regulatory authorities"),
        new(RegulationCategory, "Regulation categories"),
    ];

    public static bool IsKnown(string? category) =>
        category is not null && All.Any(c => string.Equals(c.Code, category, StringComparison.Ordinal));
}

/// <summary>A managed reference-data category: its stable <paramref name="Code"/> and human <paramref name="Label"/>.</summary>
public sealed record ReferenceDataCategoryDescriptor(string Code, string Label);
