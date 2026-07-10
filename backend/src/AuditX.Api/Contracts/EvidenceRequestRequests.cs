namespace AuditX.Api.Contracts;

/// <summary>Request a piece of expected evidence (P2-D).</summary>
public sealed record RequestEvidenceRequest(
    Guid? ChecklistItemId, string Title, string? DocumentType, DateOnly? DueDate, string? Notes);

/// <summary>Mark a requested piece of evidence as received (P2-D).</summary>
public sealed record MarkEvidenceReceivedRequest(string Version);

/// <summary>Waive a requested piece of evidence with a reason (P2-D).</summary>
public sealed record WaiveEvidenceRequest(string Reason, string Version);
