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
