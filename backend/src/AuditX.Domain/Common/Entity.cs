namespace AuditX.Domain.Common;

/// <summary>
/// Base type for every persistent entity. Uses a GUID primary key (engineering standard §10)
/// generated as a time-ordered UUIDv7 so keys are unique without a database round-trip yet remain
/// sequential for healthy clustered-index behaviour. Audit columns are populated centrally by the
/// persistence auditing interceptor rather than by callers.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}
