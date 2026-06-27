using AuditX.Domain.Common;

namespace AuditX.Domain.Sanctions;

/// <summary>
/// A stored member of a sanctions case team (M7). Membership is queryable so the confidentiality predicate
/// can unmask the subject for the investigator/recommender/HR-recorder. DC members are resolved by permission
/// at request time (not stored). Child of the <see cref="SanctionsCase"/> aggregate.
/// </summary>
public sealed class SanctionsCaseTeamMember : Entity, IBelongsToAggregate
{
    private SanctionsCaseTeamMember()
    {
    }

    public Guid SanctionsCaseId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => SanctionsCaseId;

    public Guid UserId { get; private set; }

    /// <summary>A short marker for why this user is on the team (e.g. <c>investigator</c>, <c>recommender</c>, <c>hr</c>).</summary>
    public string RoleMarker { get; private set; } = null!;

    internal SanctionsCaseTeamMember(Guid sanctionsCaseId, Guid userId, string roleMarker)
    {
        SanctionsCaseId = sanctionsCaseId;
        UserId = userId;
        RoleMarker = Guard.NotNullOrWhiteSpace(roleMarker, "sanctions.team_role_required", "A team role marker is required.");
    }
}
