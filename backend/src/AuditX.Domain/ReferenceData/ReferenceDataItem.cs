using AuditX.Domain.Common;

namespace AuditX.Domain.ReferenceData;

/// <summary>
/// A single entry in a small, user-managed reference-data list (e.g. an audit type, an exception category). The
/// generic store behind the platform's managed dropdowns: a <see cref="Category"/> discriminator groups items into
/// named lists (see <see cref="ReferenceDataCategories"/>) so new lists drop in without a schema change. <see
/// cref="Code"/> is the stable machine key stored on referencing records; <see cref="Label"/> is the human display.
/// Uniqueness is per (category, code) among live rows (filtered unique index). "Archiving" is a soft delete.
/// </summary>
public sealed class ReferenceDataItem : AggregateRoot, ISoftDeletable
{
    private ReferenceDataItem()
    {
    }

    /// <summary>The list this item belongs to (one of <see cref="ReferenceDataCategories"/>), e.g. <c>audit_type</c>.</summary>
    public string Category { get; private set; } = null!;

    /// <summary>The stable machine key stored on referencing records (immutable after creation).</summary>
    public string Code { get; private set; } = null!;

    /// <summary>The human-readable display label.</summary>
    public string Label { get; private set; } = null!;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Optimistic-concurrency token (rowversion) for safe concurrent edits.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static ReferenceDataItem Create(string category, string code, string label, string? description, int sortOrder)
        => new()
        {
            Category = Guard.NotNullOrWhiteSpace(category, "reference_data.category_required", "A reference-data category is required.").Trim(),
            Code = Guard.NotNullOrWhiteSpace(code, "reference_data.code_required", "A reference-data code is required.").Trim(),
            Label = Guard.NotNullOrWhiteSpace(label, "reference_data.label_required", "A reference-data label is required.").Trim(),
            Description = description,
            SortOrder = sortOrder,
            IsActive = true,
        };

    /// <summary>Edit the mutable fields. <see cref="Category"/> and <see cref="Code"/> are stable keys and never change.</summary>
    public void Update(string label, string? description, int sortOrder)
    {
        Label = Guard.NotNullOrWhiteSpace(label, "reference_data.label_required", "A reference-data label is required.").Trim();
        Description = description;
        SortOrder = sortOrder;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }
}
