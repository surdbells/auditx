namespace AuditX.Infrastructure.Options;

/// <summary>Evidence blob-storage configuration (bound from <c>Storage</c>).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root directory for the local-disk evidence store. Defaults to an app-relative folder.</summary>
    public string EvidenceRoot { get; set; } = string.Empty;
}
