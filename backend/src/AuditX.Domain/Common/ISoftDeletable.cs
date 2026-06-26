namespace AuditX.Domain.Common;

/// <summary>
/// Implemented by business entities that are logically (never physically) removed. A global EF Core
/// query filter excludes soft-deleted rows from normal reads; a scheduled job hard-deletes after the
/// configured grace period.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedBy { get; }

    /// <summary>Mark the entity as logically deleted.</summary>
    void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc);
}
