namespace AuditX.Application.Abstractions;

/// <summary>
/// Reads the active configuration definition for a domain (M12), deserialised to a typed shape. Backed by an
/// in-memory cache so the raise-time path and the daily scan can read it cheaply on every call; the cache is
/// invalidated whenever a version is activated or rolled back, so a new active config takes effect immediately.
/// Returns <c>null</c> (never throws) when there is no active version or it cannot be deserialised — callers fall
/// back to their hardcoded defaults so the platform never breaks on a missing/corrupt config.
/// </summary>
public interface IActiveConfigurationProvider
{
    /// <summary>Get the active definition for a domain as <typeparamref name="T"/>, or <c>null</c> if absent/unreadable.</summary>
    T? GetActive<T>(string domain) where T : class;

    /// <summary>Get the version number of the active definition for a domain, or <c>null</c> if none is active.</summary>
    int? GetActiveVersion(string domain);

    /// <summary>Drop the cached active definition + version for a domain (called post-commit on activate/rollback).</summary>
    void Invalidate(string domain);

    /// <summary>
    /// Drop every cached domain. Called post-commit after a maker-checker approval applies a captured config switch
    /// (the generic approval handler does not know which domain changed). Cheap — approvals are infrequent.
    /// </summary>
    void InvalidateAll();
}
