namespace AuditX.Application.Common.Exceptions;

/// <summary>A flagged (integrity-failed) evidence file may not be read until M11 admin review clears it. Maps to 423 Locked.</summary>
public sealed class EvidenceLockedException(string message = "This evidence file is locked pending integrity review.") : Exception(message)
{
    public string ErrorCode => "evidence_locked";
}

/// <summary>An evidence file failed its SHA-256 verification on read (US-M5-008). Maps to 500 with a stable code.</summary>
public sealed class EvidenceIntegrityException(string message = "Evidence integrity verification failed.") : Exception(message)
{
    public string ErrorCode => "evidence_hash_mismatch";
}
