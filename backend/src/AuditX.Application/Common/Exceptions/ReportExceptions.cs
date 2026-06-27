namespace AuditX.Application.Common.Exceptions;

/// <summary>
/// A stored report artefact failed its SHA-256 verification on read (US-M8-013). Maps to 500 with a stable code.
/// The verify-on-read also raises a Critical <c>report_hash_mismatch</c> integrity alert (M10) before throwing.
/// </summary>
public sealed class ReportIntegrityException(string message = "Report integrity verification failed.") : Exception(message)
{
    public string ErrorCode => "report_hash_mismatch";
}
