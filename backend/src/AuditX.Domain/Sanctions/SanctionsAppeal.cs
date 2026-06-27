using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions.Events;

namespace AuditX.Domain.Sanctions;

/// <summary>
/// An appeal against a sanctions decision (M7). Its own aggregate root. Routed to the configured appeals authority
/// (default CIA). v1 transitions <c>filed → decided</c> directly (the <c>in_review</c> dwell state is reserved).
/// </summary>
public sealed class SanctionsAppeal : AggregateRoot
{
    private SanctionsAppeal()
    {
    }

    public Guid SanctionsCaseId { get; private set; }

    public Guid AppellantUserId { get; private set; }

    public Guid RoutedToUserId { get; private set; }

    public string Basis { get; private set; } = null!;

    public AppealStatus Status { get; private set; }

    /// <summary>JSON: <c>{ outcome, detail, decided_by, decided_at }</c>.</summary>
    public string? DecisionJson { get; private set; }

    public DateTimeOffset FiledAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static SanctionsAppeal File(Guid caseId, Guid appellant, Guid routedTo, string basis, DateTimeOffset nowUtc)
    {
        var appeal = new SanctionsAppeal
        {
            SanctionsCaseId = caseId,
            AppellantUserId = appellant,
            RoutedToUserId = routedTo,
            Basis = Guard.NotNullOrWhiteSpace(basis, "sanctions.appeal_basis_required", "An appeal basis is required."),
            Status = AppealStatus.Filed,
            FiledAt = nowUtc,
        };
        appeal.RaiseDomainEvent(new AppealFiledEvent(appeal.Id, caseId, appellant, routedTo));
        return appeal;
    }

    public void RecordDecision(AppealOutcome outcome, string detailJson, Guid deciderId, DateTimeOffset nowUtc)
    {
        if (Status == AppealStatus.Decided)
        {
            throw new InvalidStateTransitionException("sanctions.appeal_already_decided", "This appeal has already been decided.");
        }

        Guard.NotNullOrWhiteSpace(detailJson, "sanctions.appeal_detail_required", "Appeal decision detail is required.");
        Status = AppealStatus.Decided;
        DecisionJson = detailJson;
        DecidedAt = nowUtc;
        RaiseDomainEvent(new AppealOutcomeRecordedEvent(Id, SanctionsCaseId, outcome, deciderId));
    }
}
