using AuditX.Domain.Common;

namespace AuditX.Domain.Compliance;

/// <summary>
/// A regulation / compliance-obligation register entry (P1-B): a regulatory requirement the bank must comply with,
/// issued by an authority (regulator). Findings link to regulations to power the compliance-by-regulation report.
/// The <c>Code</c> (e.g. "CBN-AML-2023") is a stable business key; active/retired is a lifecycle flag.
/// </summary>
public sealed class Regulation : AggregateRoot, ISoftDeletable
{
    private Regulation()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>The issuing authority / regulator (e.g. "Central Bank of Nigeria").</summary>
    public string? Authority { get; private set; }

    public string? Description { get; private set; }

    public string? Category { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static Regulation Register(string code, string name, string? authority, string? description, string? category)
        => new()
        {
            Code = Guard.NotNullOrWhiteSpace(code, "regulation.code_required", "A code is required.").Trim(),
            Name = Guard.NotNullOrWhiteSpace(name, "regulation.name_required", "A name is required.").Trim(),
            Authority = Normalise(authority),
            Description = Normalise(description),
            Category = Normalise(category),
            IsActive = true,
        };

    /// <summary>Updates the metadata. The <see cref="Code"/> is immutable.</summary>
    public void Update(string name, string? authority, string? description, string? category)
    {
        Name = Guard.NotNullOrWhiteSpace(name, "regulation.name_required", "A name is required.").Trim();
        Authority = Normalise(authority);
        Description = Normalise(description);
        Category = Normalise(category);
    }

    public void SetActive(bool active) => IsActive = active;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
