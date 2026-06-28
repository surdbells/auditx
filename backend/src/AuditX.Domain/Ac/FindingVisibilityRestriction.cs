using AuditX.Domain.Ac.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Ac;

/// <summary>
/// A per-finding visibility allow-list (M13, FR-M13-009). The CIA restricts a finding so only the listed users see
/// its detail; everyone else sees a placeholder. Unique on (<see cref="FindingType"/>, <see cref="FindingId"/>).
/// The allow-list is applied PER-REQUESTER at read time (mapper/query), never baked into the immutable pack snapshot,
/// so restricted items still count toward aggregate totals (detail hidden, counts coherent).
/// </summary>
public sealed class FindingVisibilityRestriction : AggregateRoot
{
    private FindingVisibilityRestriction()
    {
    }

    public FindingType FindingType { get; private set; }

    public Guid FindingId { get; private set; }

    /// <summary>JSON array of directory user ids permitted to see the finding's detail.</summary>
    public string AllowedUserIdsJson { get; private set; } = "[]";

    public string? Reason { get; private set; }

    public Guid RestrictedByUserId { get; private set; }

    public DateTimeOffset RestrictedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    /// <summary>Create a restriction. Raises <see cref="FindingVisibilityRestrictedEvent"/>.</summary>
    public static FindingVisibilityRestriction Create(
        FindingType findingType, Guid findingId, string allowedUserIdsJson, string? reason, Guid restrictedBy, DateTimeOffset nowUtc)
    {
        if (findingId == Guid.Empty)
        {
            throw new DomainException("finding_visibility.finding_required", "A finding id is required.");
        }

        var restriction = new FindingVisibilityRestriction
        {
            FindingType = findingType,
            FindingId = findingId,
            AllowedUserIdsJson = string.IsNullOrWhiteSpace(allowedUserIdsJson) ? "[]" : allowedUserIdsJson,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RestrictedByUserId = restrictedBy,
            RestrictedAt = nowUtc,
        };
        restriction.RaiseDomainEvent(new FindingVisibilityRestrictedEvent(findingType.ToString().ToLowerInvariant(), findingId, restrictedBy));
        return restriction;
    }

    /// <summary>Replace the allow-list / reason on an existing restriction (idempotent set-the-allow-list on re-call).</summary>
    public void Update(string allowedUserIdsJson, string? reason, Guid restrictedBy, DateTimeOffset nowUtc)
    {
        AllowedUserIdsJson = string.IsNullOrWhiteSpace(allowedUserIdsJson) ? "[]" : allowedUserIdsJson;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        RestrictedByUserId = restrictedBy;
        RestrictedAt = nowUtc;
        RaiseDomainEvent(new FindingVisibilityRestrictedEvent(FindingType.ToString().ToLowerInvariant(), FindingId, restrictedBy));
    }
}
