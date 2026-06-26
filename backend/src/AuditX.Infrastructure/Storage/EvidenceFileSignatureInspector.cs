using AuditX.Application.Abstractions.Storage;

namespace AuditX.Infrastructure.Storage;

/// <summary>
/// Validates an uploaded file's declared MIME type against the allow-list AND verifies its leading
/// magic bytes match that type, so a payload cannot be smuggled under a benign Content-Type (US-M5-009).
/// </summary>
public sealed class EvidenceFileSignatureInspector : IFileSignatureInspector
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46]], // %PDF
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47]],
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/gif"] = [[0x47, 0x49, 0x46, 0x38]], // GIF8
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [[0x50, 0x4B, 0x03, 0x04]], // PK..
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [[0x50, 0x4B, 0x03, 0x04]],
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = [[0x50, 0x4B, 0x03, 0x04]],
    };

    // Text formats have no reliable magic number; they are allowed on declared type alone.
    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/csv",
    };

    public bool IsAllowed(byte[] content, string declaredMimeType)
    {
        if (string.IsNullOrWhiteSpace(declaredMimeType))
        {
            return false;
        }

        var mime = declaredMimeType.Split(';')[0].Trim();

        if (TextTypes.Contains(mime))
        {
            return true;
        }

        if (!Signatures.TryGetValue(mime, out var candidates))
        {
            return false;
        }

        return candidates.Any(sig => content.Length >= sig.Length && content.AsSpan(0, sig.Length).SequenceEqual(sig));
    }
}
