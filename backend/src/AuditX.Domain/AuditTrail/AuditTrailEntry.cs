using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.AuditTrail;

/// <summary>
/// One immutable entry in the append-only audit trail (M11). Every state-changing operation across
/// the platform writes an entry in the SAME database transaction as the change itself, so the change
/// and its evidence commit or roll back together. Updates and deletes are rejected at the database
/// level by a trigger; this type therefore exposes no mutators.
/// </summary>
public sealed class AuditTrailEntry
{
    private AuditTrailEntry()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid? ActorUserId { get; private set; }

    public ActorType ActorType { get; private set; }

    /// <summary>Label for non-user actors (e.g. the background job or ITANDT engineer identity).</summary>
    public string? ActorSystemLabel { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? OriginatingTimezone { get; private set; }

    /// <summary>Machine-readable event type (see <see cref="AuditEventTypes"/>).</summary>
    public string EventType { get; private set; } = null!;

    public string TargetObjectType { get; private set; } = null!;

    public Guid? TargetObjectId { get; private set; }

    public string? BeforeStateJson { get; private set; }

    public string? AfterStateJson { get; private set; }

    /// <summary>Request context JSON (IP, user agent, request id).</summary>
    public string? RequestContextJson { get; private set; }

    public string? EventPayloadJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AuditTrailEntry Create(
        string eventType,
        string targetObjectType,
        Guid? targetObjectId,
        ActorType actorType,
        Guid? actorUserId,
        DateTimeOffset occurredAtUtc,
        string? actorSystemLabel = null,
        string? originatingTimezone = null,
        string? beforeStateJson = null,
        string? afterStateJson = null,
        string? requestContextJson = null,
        string? eventPayloadJson = null)
    {
        return new AuditTrailEntry
        {
            EventType = Guard.NotNullOrWhiteSpace(eventType, "audit.event_type_required", "Audit event type is required."),
            TargetObjectType = Guard.NotNullOrWhiteSpace(targetObjectType, "audit.target_type_required", "Audit target type is required."),
            TargetObjectId = targetObjectId,
            ActorType = actorType,
            ActorUserId = actorUserId,
            ActorSystemLabel = actorSystemLabel,
            OccurredAtUtc = occurredAtUtc,
            OriginatingTimezone = originatingTimezone,
            BeforeStateJson = beforeStateJson,
            AfterStateJson = afterStateJson,
            RequestContextJson = requestContextJson,
            EventPayloadJson = eventPayloadJson,
            CreatedAt = occurredAtUtc,
        };
    }
}
