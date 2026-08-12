namespace AuditX.Api.Contracts;

public sealed record SubmitResponseRequest(string? Verdict, string? Comment, string? ValueJson, bool IsDraft, string Version, string? Observation = null, string? Recommendation = null);

public sealed record AssignItemRequest(Guid? AssigneeUserId, string Version);

public sealed record BulkAssignmentRequest(Guid ItemId, Guid? AssigneeUserId);

public sealed record BulkReassignRequest(IReadOnlyList<BulkAssignmentRequest> Assignments, string Version);

public sealed record FailJudgementRequest(string Justification, string Version);
