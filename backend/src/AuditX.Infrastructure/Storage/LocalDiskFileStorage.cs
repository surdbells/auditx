using AuditX.Application.Abstractions.Storage;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Storage;

/// <summary>
/// On-premises evidence store backed by the local filesystem. Keys are opaque relative paths; the root
/// is configured via <see cref="StorageOptions"/>. SMB/NFS/S3 are deployment-time swaps behind <see cref="IFileStorage"/>.
/// </summary>
public sealed class LocalDiskFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalDiskFileStorage(IOptions<StorageOptions> options)
    {
        var configured = options.Value.EvidenceRoot;
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "evidence-store")
            : configured;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string key, byte[] content, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return key;
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        => await File.ReadAllBytesAsync(Resolve(key), cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(key);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        // Defend against path traversal: the resolved path must stay strictly under the configured root.
        // Compare against the root WITH a trailing separator so a sibling whose name shares the root as a
        // prefix (e.g. "evidence-store-x") cannot pass a naive StartsWith check.
        var fullPath = Path.GetFullPath(Path.Combine(_root, key.Replace('\\', '/').TrimStart('/')));
        var rootBoundary = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootBoundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved storage path escapes the evidence root.");
        }

        return fullPath;
    }
}
