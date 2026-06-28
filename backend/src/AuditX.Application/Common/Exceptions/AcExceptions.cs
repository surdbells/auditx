namespace AuditX.Application.Common.Exceptions;

/// <summary>
/// A stored AC-pack artefact failed its SHA-256 verification on read (M13). Maps to 500 with a stable code.
/// The verify-on-read also raises a Critical <c>ac_pack_hash_mismatch</c> integrity alert (M10) before throwing.
/// </summary>
public sealed class AcPackIntegrityException(string message = "AC pack integrity verification failed.") : Exception(message)
{
    public string ErrorCode => "ac_pack_hash_mismatch";
}
