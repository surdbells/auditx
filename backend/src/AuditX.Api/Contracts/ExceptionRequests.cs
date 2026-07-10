namespace AuditX.Api.Contracts;

public sealed record RaiseExceptionRequest(
    Guid ChecklistItemId, string Title, string Severity, string RootCause, string Recommendation,
    string? Category, string? RootCauseCategory, Guid OwnerUserId, DateOnly? TargetDateOverride, string? OverrideRationale);

public sealed record ChangeSeverityRequest(string Severity, string Reason, string Version);

public sealed record ReassignOwnerRequest(Guid OwnerUserId, string Version);

public sealed record MapActionRequest(string Description, Guid OwnerUserId, DateOnly TargetDate, string? ExpectedEvidenceType);

public sealed record SubmitMapRequest(IReadOnlyList<MapActionRequest> Actions, string Version);

public sealed record VersionOnlyRequest(string Version);

public sealed record ReasonVersionRequest(string Reason, string Version);

public sealed record CloseExceptionRequest(string? ClosureNote, string Version);
