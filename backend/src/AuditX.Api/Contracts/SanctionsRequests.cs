namespace AuditX.Api.Contracts;

public sealed record TriggerSanctionsRequest(Guid? SubjectUserId);

public sealed record RecordRecommendationRequest(string Recommendation, string? DeviationReason, string Version);

public sealed record RecordHrOutcomeRequest(string OutcomeType, string Detail, Guid? EvidenceFileId, string Version);

public sealed record ReferToDcRequest(string ReferralReason, string Version);

public sealed record RecordDcDecisionRequest(string Decision, string Rationale, string? VotingRecord, string Version);

public sealed record FileAppealRequest(string Basis, Guid? EvidenceFileId, string Version);

public sealed record DecideAppealRequest(string Outcome, string Rationale, string Version);

public sealed record CreateGridVersionRequest(string GridDefinition);

public sealed record ActivateGridVersionRequest(string ActivationReason);
