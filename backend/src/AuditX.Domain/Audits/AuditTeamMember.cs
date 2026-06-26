using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>A member of an audit's team (M4). Removal is a soft-remove (<see cref="RemovedAt"/> set).</summary>
public sealed class AuditTeamMember : Entity, IBelongsToAggregate
{
    private AuditTeamMember()
    {
    }

    public Guid AuditId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AuditId;

    public Guid UserId { get; private set; }

    public TeamRole TeamRole { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public Guid? AddedBy { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public bool IsActive => RemovedAt is null;

    internal AuditTeamMember(Guid auditId, Guid userId, TeamRole teamRole, Guid? addedBy, DateTimeOffset addedAt)
    {
        AuditId = auditId;
        UserId = userId;
        TeamRole = teamRole;
        AddedBy = addedBy;
        AddedAt = addedAt;
    }

    internal void Remove(DateTimeOffset removedAt) => RemovedAt = removedAt;

    internal void ChangeRole(TeamRole role) => TeamRole = role;
}
