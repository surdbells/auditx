using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Reports;

/// <summary>
/// A single dispatch of a completed report to one recipient (M8). Child of the <see cref="Report"/> aggregate.
/// Exactly one of <see cref="RecipientUserId"/> / <see cref="RecipientEmail"/> is set (domain guard) so the row
/// captures either a directory user or an ad-hoc external address. <see cref="Outcome"/> starts
/// <see cref="DeliveryOutcome.Pending"/> (= accepted by the SMTP relay, final delivery unknown) and is confirmed
/// Delivered / Bounced through <see cref="Report.RecordDistributionOutcome"/> — by a distributor, or by an
/// email-provider integration calling the same endpoint with a service account.
/// </summary>
public sealed class ReportDistribution : Entity, IBelongsToAggregate
{
    private ReportDistribution()
    {
    }

    public Guid ReportId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => ReportId;

    /// <summary>Snapshot of the report version that was distributed.</summary>
    public int ReportVersionNumber { get; private set; }

    public Guid? RecipientUserId { get; private set; }

    public string? RecipientEmail { get; private set; }

    public DateTimeOffset DispatchedAt { get; private set; }

    public Guid DispatchedBy { get; private set; }

    public DeliveryOutcome Outcome { get; private set; } = DeliveryOutcome.Pending;

    /// <summary>When the outcome was confirmed (null while pending).</summary>
    public DateTimeOffset? OutcomeRecordedAt { get; private set; }

    /// <summary>Who confirmed the outcome (null while pending).</summary>
    public Guid? OutcomeRecordedBy { get; private set; }

    /// <summary>
    /// Confirm the delivery outcome (Pending → Delivered | Bounced). A confirmed outcome is final — the
    /// distribution log is evidence, so it is never rewritten.
    /// </summary>
    internal void RecordOutcome(DeliveryOutcome outcome, Guid byUserId, DateTimeOffset atUtc)
    {
        if (outcome == DeliveryOutcome.Pending)
        {
            throw new DomainException("report.outcome_invalid", "The outcome must be delivered or bounced.");
        }

        if (Outcome != DeliveryOutcome.Pending)
        {
            throw new InvalidStateTransitionException(
                "report.outcome_already_recorded", "This distribution's outcome has already been confirmed.");
        }

        Outcome = outcome;
        OutcomeRecordedBy = byUserId;
        OutcomeRecordedAt = atUtc;
    }

    internal ReportDistribution(
        Guid reportId, int reportVersionNumber, Guid? recipientUserId, string? recipientEmail,
        Guid dispatchedBy, DateTimeOffset dispatchedAt)
    {
        var hasUser = recipientUserId is { } uid && uid != Guid.Empty;
        var hasEmail = !string.IsNullOrWhiteSpace(recipientEmail);
        if (hasUser == hasEmail)
        {
            throw new DomainException(
                "report.distribution_recipient_invalid",
                "A distribution must target exactly one of a directory user or an email address.");
        }

        ReportId = reportId;
        ReportVersionNumber = reportVersionNumber;
        RecipientUserId = hasUser ? recipientUserId : null;
        RecipientEmail = hasEmail ? recipientEmail!.Trim() : null;
        DispatchedBy = dispatchedBy;
        DispatchedAt = dispatchedAt;
    }
}
