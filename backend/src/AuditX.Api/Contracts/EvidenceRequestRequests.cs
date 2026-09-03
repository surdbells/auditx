namespace AuditX.Api.Contracts;

/// <summary>Request a document/evidence from an auditee (P2-D). Purpose: review_document or finding_evidence.</summary>
public sealed record RequestEvidenceRequest(
    Guid RequestedFromUserId, string Title, string? DocumentType, DateOnly? DueDate, string? Notes,
    Guid? ChecklistItemId = null, Guid? ExceptionId = null, string? Purpose = null);

/// <summary>Mark a requested piece of evidence as received (P2-D).</summary>
public sealed record MarkEvidenceReceivedRequest(string Version);

/// <summary>Waive a requested piece of evidence with a reason (P2-D).</summary>
public sealed record WaiveEvidenceRequest(string Reason, string Version);
