using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence.Events;

namespace AuditX.Domain.Evidence;

/// <summary>
/// A request for a specific piece of audit evidence (P2-D): the "expected / requested" side of evidence, distinct
/// from the uploaded <c>EvidenceFile</c> ("received"). The auditor addresses it to an auditee, who is emailed a
/// link and uploads the document — which marks the request received and surfaces on the auditor's screen.
/// Outstanding until an upload arrives (or the auditor marks it received / waives it) — the substrate for
/// missing / pending-evidence and evidence-by-type reporting. A standalone soft-deletable aggregate; rowversion-guarded.
/// </summary>
public sealed class EvidenceRequest : AggregateRoot, ISoftDeletable
{
    private EvidenceRequest()
    {
    }

    public Guid AuditId { get; private set; }

    /// <summary>Optional link to the checklist item this evidence supports.</summary>
    public Guid? ChecklistItemId { get; private set; }

    /// <summary>Optional link to the finding this evidence backs (when <see cref="Purpose"/> is FindingEvidence).</summary>
    public Guid? ExceptionId { get; private set; }

    /// <summary>Whether this is a general review document or evidence that backs a finding.</summary>
    public EvidenceRequestPurpose Purpose { get; private set; }

    /// <summary>The auditee the document is requested from (emailed the upload link).</summary>
    public Guid RequestedFromUserId { get; private set; }

    public string Title { get; private set; } = null!;

    /// <summary>Document-type taxonomy code (from the <c>evidence_document_type</c> reference-data list).</summary>
    public string? DocumentType { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateOnly RequestedOn { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public EvidenceRequestStatus Status { get; private set; }

    public Guid? ReceivedByUserId { get; private set; }

    public DateTimeOffset? ReceivedAt { get; private set; }

    public string? WaiveReason { get; private set; }

    public string? Notes { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static EvidenceRequest Request(
        Guid auditId, string auditName, Guid? checklistItemId, Guid? exceptionId, EvidenceRequestPurpose purpose,
        string title, string? documentType, Guid requestedByUserId, Guid requestedFromUserId,
        DateOnly requestedOn, DateOnly? dueDate, string? notes)
    {
        Guard.Against(auditId == Guid.Empty, "evidence_request.audit_required", "An audit is required.");
        Guard.Against(requestedByUserId == Guid.Empty, "evidence_request.requester_required", "A requester is required.");
        Guard.Against(requestedFromUserId == Guid.Empty, "evidence_request.recipient_required", "The auditee to request from is required.");

        var request = new EvidenceRequest
        {
            AuditId = auditId,
            ChecklistItemId = checklistItemId,
            ExceptionId = exceptionId,
            Purpose = purpose,
            Title = Guard.NotNullOrWhiteSpace(title, "evidence_request.title_required", "A title is required."),
            DocumentType = Normalise(documentType),
            RequestedByUserId = requestedByUserId,
            RequestedFromUserId = requestedFromUserId,
            RequestedOn = requestedOn,
            DueDate = dueDate,
            Status = EvidenceRequestStatus.Requested,
            Notes = Normalise(notes),
        };

        // Notify the auditee (email with an upload link) via the M10 pipeline.
        request.RaiseDomainEvent(new EvidenceRequestedEvent(
            request.Id, auditId, auditName, requestedFromUserId, requestedByUserId, request.Title, dueDate?.ToString("yyyy-MM-dd")));
        return request;
    }

    /// <summary>
    /// Records that the auditee uploaded a document against this request. The first upload moves the request from
    /// Requested to Received (attributed to the uploader); further uploads add more files without re-transitioning.
    /// A waived request cannot receive uploads.
    /// </summary>
    public void RecordUpload(Guid uploadedByUserId, DateTimeOffset nowUtc)
    {
        if (Status == EvidenceRequestStatus.Waived)
        {
            throw new InvalidStateTransitionException("evidence_request.waived", "This request was waived and can no longer receive documents.");
        }

        if (Status == EvidenceRequestStatus.Requested)
        {
            Status = EvidenceRequestStatus.Received;
            ReceivedByUserId = uploadedByUserId;
            ReceivedAt = nowUtc;
        }
    }

    /// <summary>Marks the requested evidence as received (only from Requested).</summary>
    public void MarkReceived(Guid userId, DateTimeOffset nowUtc)
    {
        EnsureRequested("evidence_request.not_outstanding");
        Status = EvidenceRequestStatus.Received;
        ReceivedByUserId = userId;
        ReceivedAt = nowUtc;
    }

    /// <summary>
    /// Formally waives the request with a reason (only from Requested), e.g. evidence no longer applicable.
    /// A waived request was never received, so the received fields stay null; the waiver's actor and timestamp
    /// are captured in the append-only audit trail.
    /// </summary>
    public void Waive(string reason)
    {
        EnsureRequested("evidence_request.not_outstanding");
        WaiveReason = Guard.NotNullOrWhiteSpace(reason, "evidence_request.waive_reason_required", "A waiver reason is required.");
        Status = EvidenceRequestStatus.Waived;
    }

    /// <summary>True when still outstanding and past its due date (drives overdue-evidence reporting).</summary>
    public bool IsOverdue(DateOnly today)
        => Status == EvidenceRequestStatus.Requested && DueDate is { } due && due < today;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private void EnsureRequested(string code)
    {
        if (Status != EvidenceRequestStatus.Requested)
        {
            throw new InvalidStateTransitionException(code, "The evidence request is no longer outstanding.");
        }
    }

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
