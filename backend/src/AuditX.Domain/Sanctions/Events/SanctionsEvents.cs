using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Sanctions.Events;

public abstract record SanctionsEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SanctionsTriggeredEvent(Guid SanctionsCaseId, Guid ExceptionId, Guid? SubjectUserId, Guid TriggeredBy) : SanctionsEvent;

public sealed record SanctionsRecommendedEvent(Guid SanctionsCaseId, int? GridConsultedVersion, bool WithinGridRange, Guid RecommendedBy) : SanctionsEvent;

public sealed record SanctionsRecommendationSubmittedEvent(Guid SanctionsCaseId, Guid SubmittedBy) : SanctionsEvent;

public sealed record HrOutcomeRecordedEvent(Guid SanctionsCaseId, HrOutcomeType OutcomeType, Guid RecordedBy) : SanctionsEvent;

public sealed record DcReferralEvent(Guid SanctionsCaseId, Guid ReferredBy) : SanctionsEvent;

public sealed record DcDecisionRecordedEvent(Guid SanctionsCaseId, DcDecisionType Decision, Guid DecidedBy) : SanctionsEvent;

public sealed record AppealFiledEvent(Guid AppealId, Guid SanctionsCaseId, Guid AppellantUserId, Guid RoutedToUserId) : SanctionsEvent;

public sealed record AppealOutcomeRecordedEvent(Guid AppealId, Guid SanctionsCaseId, AppealOutcome Outcome, Guid DecidedBy) : SanctionsEvent;

public sealed record SanctionsCaseClosedEvent(Guid SanctionsCaseId, Guid ClosedBy) : SanctionsEvent;

public sealed record GridVersionCreatedEvent(Guid GridVersionId, int VersionNumber, Guid CreatedBy) : SanctionsEvent;

public sealed record GridVersionActivatedEvent(Guid GridVersionId, int VersionNumber, Guid ActivatedBy) : SanctionsEvent;
