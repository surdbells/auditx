using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence.Events;

namespace AuditX.Domain.Evidence;

/// <summary>
/// An uploaded evidence file (M5). Its own aggregate root: independent lifecycle (hash-verify on read,
/// integrity flag, soft-delete for retention) and linked to its context (a checklist response in M5)
/// via <see cref="ContextType"/> + <see cref="ContextId"/>. The SHA-256 is computed while streaming on
/// upload and re-verified on every read.
/// </summary>
public sealed class EvidenceFile : AggregateRoot, ISoftDeletable
{
    private EvidenceFile()
    {
    }

    public Guid AuditId { get; private set; }

    public EvidenceContextType ContextType { get; private set; }

    public Guid ContextId { get; private set; }

    public string StoragePath { get; private set; } = null!;

    public string OriginalFilename { get; private set; } = null!;

    public string MimeType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public string Sha256Hash { get; private set; } = null!;

    public Guid UploadedBy { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>Set when a read detects a hash mismatch; blocks all subsequent reads until M11 unflags it.</summary>
    public bool IsFlagged { get; private set; }

    /// <summary>
    /// When the person who provided this document RECALLED it (took it back — e.g. the wrong file was sent). A
    /// recalled document stops counting as evidence but is never removed: the record, its hash and the stored bytes
    /// are all kept, so what was provided and that it was withdrawn both remain on the record. Null = not recalled.
    /// </summary>
    public DateTimeOffset? RecalledAt { get; private set; }

    public Guid? RecalledBy { get; private set; }

    /// <summary>Why it was recalled — mandatory, so a withdrawal is always explained.</summary>
    public string? RecallReason { get; private set; }

    public bool IsRecalled => RecalledAt is not null;

    public string? DeletionReason { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static EvidenceFile Create(
        Guid auditId, EvidenceContextType contextType, Guid contextId, string storagePath, string originalFilename,
        string mimeType, long sizeBytes, string sha256Hash, Guid uploadedBy, DateTimeOffset uploadedAt)
    {
        var file = new EvidenceFile
        {
            AuditId = auditId,
            ContextType = contextType,
            ContextId = contextId,
            StoragePath = Guard.NotNullOrWhiteSpace(storagePath, "evidence.storage_path_required", "Storage path is required."),
            OriginalFilename = Guard.NotNullOrWhiteSpace(originalFilename, "evidence.filename_required", "Filename is required."),
            MimeType = Guard.NotNullOrWhiteSpace(mimeType, "evidence.mime_required", "MIME type is required."),
            SizeBytes = sizeBytes,
            Sha256Hash = Guard.NotNullOrWhiteSpace(sha256Hash, "evidence.hash_required", "Hash is required."),
            UploadedBy = uploadedBy,
            UploadedAt = uploadedAt,
        };
        file.RaiseDomainEvent(new EvidenceUploadedEvent(file.Id, auditId, contextType, contextId, uploadedBy));
        return file;
    }

    /// <summary>Flag the file as failing integrity verification (US-M5-008).</summary>
    public void Flag()
    {
        if (IsFlagged)
        {
            return;
        }

        IsFlagged = true;
        RaiseDomainEvent(new EvidenceHashMismatchEvent(Id, AuditId, Sha256Hash));
    }

    /// <summary>Clear an integrity flag after investigation (US-M11 evidence unflag); reason is mandatory.</summary>
    public void ClearFlag(string resolution)
    {
        if (!IsFlagged)
        {
            throw new DomainException("evidence.not_flagged", "The evidence file is not flagged.");
        }

        Guard.NotNullOrWhiteSpace(resolution, "evidence.resolution_required", "A resolution note is required to unflag.");
        IsFlagged = false;
        RaiseDomainEvent(new EvidenceUnflaggedEvent(Id, AuditId, resolution));
    }

    /// <summary>Upper bound on a recall reason.</summary>
    public const int MaxRecallReasonLength = 1000;

    /// <summary>
    /// Recall a document that was uploaded in answer to an evidence request. Only such documents can be recalled —
    /// evidence an auditor attached to a response is the audit's own working paper. Irreversible: to provide the
    /// document again the auditee uploads a new one, leaving both on the record.
    /// </summary>
    public void Recall(string reason, Guid recalledBy, DateTimeOffset nowUtc)
    {
        if (ContextType != EvidenceContextType.EvidenceRequest)
        {
            throw new DomainException("evidence.recall_not_supported", "Only a document provided for an evidence request can be recalled.");
        }

        if (IsRecalled)
        {
            throw new InvalidStateTransitionException("evidence.already_recalled", "This document has already been recalled.");
        }

        var trimmed = Guard.NotNullOrWhiteSpace(reason, "evidence.recall_reason_required", "A reason is required to recall a document.").Trim();
        Guard.Against(trimmed.Length > MaxRecallReasonLength, "evidence.recall_reason_too_long", $"A recall reason can be at most {MaxRecallReasonLength} characters.");

        RecallReason = trimmed;
        RecalledBy = recalledBy;
        RecalledAt = nowUtc;
    }

    // Explicit interface implementation so the reason-less path is not part of the public surface;
    // application code must use the reason-bearing overload (US-M5-011 mandatory reason).
    void ISoftDeletable.SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc) => MarkDeleted(deletedBy, deletedAtUtc);

    /// <summary>Soft-delete with a mandatory reason (US-M5-011).</summary>
    public void SoftDelete(string reason, Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        DeletionReason = Guard.NotNullOrWhiteSpace(reason, "evidence.reason_required", "A deletion reason is required.");
        MarkDeleted(deletedBy, deletedAtUtc);
    }

    private void MarkDeleted(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        DeletedBy = deletedBy;
    }
}
