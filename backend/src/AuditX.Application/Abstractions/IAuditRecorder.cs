using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions;

/// <summary>
/// Records audit-trail entries (M11). The entry is enqueued onto the current unit of work and
/// therefore commits in the SAME transaction as the state change it describes. Actor and request
/// context are resolved automatically from the ambient request.
/// </summary>
public interface IAuditRecorder
{
    void Record(
        string eventType,
        string targetObjectType,
        Guid? targetObjectId,
        object? before = null,
        object? after = null,
        object? payload = null);

    /// <summary>Record an entry attributed to a non-user actor (system jobs, support channel).</summary>
    void RecordAs(
        ActorType actorType,
        string? actorSystemLabel,
        Guid? actorUserId,
        string eventType,
        string targetObjectType,
        Guid? targetObjectId,
        object? before = null,
        object? after = null,
        object? payload = null);
}
