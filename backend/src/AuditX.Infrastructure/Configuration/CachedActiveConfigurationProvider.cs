using AuditX.Application.Abstractions;
using AuditX.Application.Common.Json;
using AuditX.Application.Configuration;
using AuditX.Domain.Common;
using AuditX.Domain.Configuration;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Configuration;

/// <summary>
/// Reads the active configuration definition for a domain (M12) and caches it in <see cref="IMemoryCache"/>. The
/// cache is invalidated explicitly by the activate/rollback switch (see ConfigurationActivation.SwitchAsync) so a new
/// active version takes effect immediately. Reads are synchronous (the interface is sync) — a short-lived cache miss
/// hits the DB once and caches the raw definition JSON + version for the domain. Never throws: a missing/corrupt
/// active version returns <c>null</c> so callers fall back to their hardcoded defaults.
/// </summary>
public sealed class CachedActiveConfigurationProvider(
    AppDbContext db, IMemoryCache cache, ILogger<CachedActiveConfigurationProvider> logger)
    : IActiveConfigurationProvider
{
    private static string CacheKey(string domain) => $"active-config:{domain}";

    // Defence-in-depth: even if an explicit invalidation is ever missed, a cached entry self-heals within this window.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    // Track the live cache keys so InvalidateAll can clear them (IMemoryCache has no enumerate/clear-by-prefix).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> Keys = new();

    public T? GetActive<T>(string domain) where T : class
    {
        var entry = Load(domain);
        if (entry?.DefinitionJson is not { } json)
        {
            return null;
        }

        try
        {
            // exception_defaults has a typed parser (with range validation) — use it so the provider returns the same
            // shape the validators enforce. Other domains fall back to permissive web JSON.
            if (domain == ConfigurationDomains.ExceptionDefaults && typeof(T) == typeof(ExceptionDefaultsDefinition))
            {
                return ConfigurationDefinitions.ParseExceptionDefaults(json) as T;
            }

            return System.Text.Json.JsonSerializer.Deserialize<T>(json, AppJson.Options);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or DomainException)
        {
            logger.LogWarning(ex, "Active configuration for domain {Domain} could not be deserialized; falling back.", domain);
            return null;
        }
    }

    public int? GetActiveVersion(string domain) => Load(domain)?.VersionNumber;

    public void Invalidate(string domain) => cache.Remove(CacheKey(domain));

    public void InvalidateAll()
    {
        foreach (var key in Keys.Keys)
        {
            cache.Remove(key);
        }
    }

    private CacheEntry? Load(string domain)
    {
        var key = CacheKey(domain);
        return cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            Keys.TryAdd(key, 0);
            return db.BankConfigurations.AsNoTracking()
                .Where(c => c.Domain == domain && c.IsActive)
                .Select(c => new CacheEntry(c.VersionNumber, c.DefinitionJson))
                .FirstOrDefault();
        });
    }

    private sealed record CacheEntry(int VersionNumber, string DefinitionJson);
}
