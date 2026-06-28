using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Ac;

/// <summary>
/// A single dispatch of an approved AC pack to one audit-committee recipient (M13). Child of the
/// <see cref="AcPack"/> aggregate. The AC cohort is a set of directory users (the deferred external-NED token
/// access has no entity yet), so every distribution targets a <see cref="RecipientUserId"/>.
/// <see cref="Outcome"/> defaults to <see cref="AcDeliveryOutcome.Pending"/>; the M14 delivery callback is deferred.
/// </summary>
public sealed class AcPackDistribution : Entity, IBelongsToAggregate
{
    private AcPackDistribution()
    {
    }

    public Guid AcPackId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AcPackId;

    /// <summary>Snapshot of the pack version that was distributed.</summary>
    public int AcPackVersionNumber { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public DateTimeOffset DispatchedAt { get; private set; }

    public Guid DispatchedBy { get; private set; }

    public AcDeliveryOutcome Outcome { get; private set; } = AcDeliveryOutcome.Pending;

    internal AcPackDistribution(Guid acPackId, int acPackVersionNumber, Guid recipientUserId, Guid dispatchedBy, DateTimeOffset dispatchedAt)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new DomainException("ac_pack.distribution_recipient_required", "A distribution must target a directory user.");
        }

        AcPackId = acPackId;
        AcPackVersionNumber = acPackVersionNumber;
        RecipientUserId = recipientUserId;
        DispatchedBy = dispatchedBy;
        DispatchedAt = dispatchedAt;
    }
}
