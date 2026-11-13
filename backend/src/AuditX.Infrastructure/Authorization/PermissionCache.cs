using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuditX.Infrastructure.Authorization;

/// <summary>
/// The Redis keys behind the effective-permission cache, and the one operation everything else needs from it:
/// dropping a user's entry. Kept apart from <see cref="PermissionResolver"/> because it must be usable from the
/// save-changes interceptor, which the resolver's DbContext itself depends on (a resolver there would be a cycle).
/// Depends only on the singleton multiplexer and options, so it is safe as a singleton.
/// </summary>
public sealed class PermissionCache(IConnectionMultiplexer redis, IOptions<RedisOptions> redisOptions)
{
    private readonly string _instance = redisOptions.Value.InstanceName;

    public IDatabase Database => redis.GetDatabase();

    public RedisKey VersionKey() => $"{_instance}perm:ver";

    public RedisKey UserKey(long version, Guid userId) => $"{_instance}perm:{version}:{userId}";

    public async Task<long> GetVersionAsync()
    {
        var value = await Database.StringGetAsync(VersionKey());
        return value.HasValue && long.TryParse(value.ToString(), out var version) ? version : 0;
    }

    /// <summary>Drops one user's cached permissions so their next request recomputes them.</summary>
    public async Task InvalidateUserAsync(Guid userId)
        => await Database.KeyDeleteAsync(UserKey(await GetVersionAsync(), userId));
}
