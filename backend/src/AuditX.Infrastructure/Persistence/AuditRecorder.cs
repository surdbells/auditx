using AuditX.Application.Abstractions;
using AuditX.Application.Common.Json;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Infrastructure.Persistence;

/// <summary>
/// Enqueues audit-trail entries onto the current <see cref="AppDbContext"/>. Because the entry is added
/// to the same change tracker, it commits in the SAME transaction as the state change it records
/// (M11; BR-M1-012). Actor and request context are resolved from the ambient request.
/// </summary>
public sealed class AuditRecorder(
    AppDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IRequestContext requestContext)
    : IAuditRecorder
{
    public void Record(
        string eventType,
        string targetObjectType,
        Guid? targetObjectId,
        object? before = null,
        object? after = null,
        object? payload = null)
        => RecordAs(ActorType.User, null, currentUser.UserId, eventType, targetObjectType, targetObjectId, before, after, payload);

    public void RecordAs(
        ActorType actorType,
        string? actorSystemLabel,
        Guid? actorUserId,
        string eventType,
        string targetObjectType,
        Guid? targetObjectId,
        object? before = null,
        object? after = null,
        object? payload = null)
    {
        var entry = AuditTrailEntry.Create(
            eventType,
            targetObjectType,
            targetObjectId,
            actorType,
            actorUserId,
            clock.UtcNow,
            actorSystemLabel,
            originatingTimezone: null,
            beforeStateJson: Serialize(before),
            afterStateJson: Serialize(after),
            requestContextJson: SerializeRequestContext(),
            eventPayloadJson: Serialize(payload));

        db.AuditTrail.Add(entry);
    }

    private static string? Serialize(object? value) => value is null ? null : AppJson.Serialize(value);

    private string SerializeRequestContext() => AppJson.Serialize(new
    {
        requestContext.RequestId,
        requestContext.IpAddress,
        requestContext.UserAgent,
    });
}
