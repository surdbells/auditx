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

    /// <summary>Every category the store recognises, with its human label — the admin UI's manageable-list menu.</summary>
    public static readonly IReadOnlyList<ReferenceDataCategoryDescriptor> All =
    [
        new(AuditType, "Audit types"),
        new(ExceptionCategory, "Exception categories"),
    ];

    public static bool IsKnown(string? category) =>
        category is not null && All.Any(c => string.Equals(c.Code, category, StringComparison.Ordinal));
}

/// <summary>A managed reference-data category: its stable <paramref name="Code"/> and human <paramref name="Label"/>.</summary>
public sealed record ReferenceDataCategoryDescriptor(string Code, string Label);
