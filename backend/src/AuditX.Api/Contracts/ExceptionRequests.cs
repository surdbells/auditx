namespace AuditX.Api.Contracts;

public sealed record RaiseExceptionRequest(
    Guid ChecklistItemId, string Title, string Severity, string RootCause, string Recommendation,
    string? Category, string? RootCauseCategory, Guid OwnerUserId, DateOnly? TargetDateOverride, string? OverrideRationale,
    string? NonConformanceCategory = null);

public sealed record ChangeSeverityRequest(string Severity, string Reason, string Version);

// P2-B — management response, follow-up verification (reopen reuses ReasonVersionRequest).
public sealed record ManagementResponseRequest(string Decision, string Comment, string Version);

/// <summary>Sets (or clears, with a null date) the management-response due date for response-timeliness reporting.</summary>
public sealed record SetResponseDueDateRequest(DateOnly? DueDate, string Version);

public sealed record AddVerificationRequest(string Result, string? Notes, string Version);

public sealed record ReassignOwnerRequest(Guid OwnerUserId, string Version);

public sealed record MapActionRequest(string Description, Guid OwnerUserId, DateOnly TargetDate, string? ExpectedEvidenceType);

public sealed record SubmitMapRequest(IReadOnlyList<MapActionRequest> Actions, string Version);

public sealed record VersionOnlyRequest(string Version);

public sealed record ReasonVersionRequest(string Reason, string Version);

public sealed record CloseExceptionRequest(string? ClosureNote, string Version);
