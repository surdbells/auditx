namespace AuditX.Application.Abstractions.Persistence;

/// <summary>A read-only projection of an audit-trail entry (M11 core; surfaced by per-object history endpoints).</summary>
public sealed record AuditTrailEntryView(
    Guid Id,
    string EventType,
    string TargetObjectType,
    Guid? TargetObjectId,
    Guid? ActorUserId,
    string ActorType,
    DateTimeOffset OccurredAtUtc,
    string? BeforeStateJson,
    string? AfterStateJson,
    string? EventPayloadJson);

/// <summary>Reads the append-only audit trail for per-object history surfaces.</summary>
public interface IAuditTrailReader
{
    Task<IReadOnlyList<AuditTrailEntryView>> GetForTargetAsync(
        string targetObjectType, Guid targetObjectId, string? eventType, int limit, CancellationToken cancellationToken = default);
}
