using AuditX.Application.Common.Models;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>A read-only projection of an audit-trail entry (M11). Carries the investigator fields needed
/// to reconstruct who/what/when, including request context (IP/UA), timezone, and before/after state.</summary>
public sealed record AuditTrailEntryView(
    Guid Id,
    string EventType,
    string TargetObjectType,
    Guid? TargetObjectId,
    Guid? ActorUserId,
    string ActorType,
    string? ActorSystemLabel,
    DateTimeOffset OccurredAtUtc,
    string? OriginatingTimezone,
    string? BeforeStateJson,
    string? AfterStateJson,
    string? RequestContextJson,
    string? EventPayloadJson);

/// <summary>Filter for the general audit-trail query surface (M11). All dimensions are optional (AND-combined).</summary>
public sealed record AuditTrailFilter(
    Guid? ActorUserId = null,
    string? EventType = null,
    string? TargetObjectType = null,
    Guid? TargetObjectId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

/// <summary>Reads the append-only audit trail: general filtered query (M11) + per-object history.</summary>
public interface IAuditTrailReader
{
    Task<IReadOnlyList<AuditTrailEntryView>> GetForTargetAsync(
        string targetObjectType, Guid targetObjectId, string? eventType, int limit, CancellationToken cancellationToken = default);

    /// <summary>Filtered, keyset-cursor page over the trail, ordered newest-first.</summary>
    Task<CursorPage<AuditTrailEntryView>> QueryAsync(AuditTrailFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Stream all entries matching the filter, newest-first, for export (no buffering).</summary>
    IAsyncEnumerable<AuditTrailEntryView> StreamAsync(AuditTrailFilter filter, CancellationToken cancellationToken = default);
}
