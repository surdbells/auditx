namespace AuditX.Application.Abstractions.Storage;

/// <summary>
/// Binary blob store for evidence files. On-prem default is local disk; SMB/NFS/S3 are deployment-time
/// swaps behind this port (M14 concern). Keys are opaque relative paths chosen by the caller.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string key, byte[] content, CancellationToken cancellationToken = default);

    Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Validates an uploaded file's declared MIME type against the allow-list AND its magic bytes (US-M5-009).</summary>
public interface IFileSignatureInspector
{
    bool IsAllowed(byte[] content, string declaredMimeType);
}
