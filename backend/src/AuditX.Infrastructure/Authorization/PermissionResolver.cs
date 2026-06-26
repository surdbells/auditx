using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Json;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Options;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuditX.Infrastructure.Authorization;

/// <summary>
/// Resolves a user's effective permissions — the transitive union of role assignments, role
/// inheritance and active delegations — and caches them in Redis (5-minute TTL). A global version
/// counter enables O(1) invalidation of every user's cache when a role definition changes.
/// </summary>
public sealed class PermissionResolver(
    AppDbContext db,
    IConnectionMultiplexer redis,
    IClock clock,
    IOptions<RedisOptions> redisOptions)
    : IPermissionResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly string _instance = redisOptions.Value.InstanceName;

    public async Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var version = await GetVersionAsync(database);
        var cacheKey = UserKey(version, userId);

        var cached = await database.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            return AppJson.Deserialize<List<EffectivePermission>>(cached!);
        }

        var computed = await ComputeAsync(userId, cancellationToken);
        await database.StringSetAsync(cacheKey, AppJson.Serialize(computed), CacheTtl);
        return computed;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionKey, string? scopeValue = null, CancellationToken cancellationToken = default)
    {
        var permissions = await GetEffectivePermissionsAsync(userId, cancellationToken);
        return permissions.Any(p => p.Key == permissionKey && Satisfies(p, scopeValue));
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var version = await GetVersionAsync(database);
        await database.KeyDeleteAsync(UserKey(version, userId));
    }

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
        => await redis.GetDatabase().StringIncrementAsync(VersionKey());

    private static bool Satisfies(EffectivePermission permission, string? scopeValue)
        => permission.ScopeType == PermissionScopeType.Global
           || scopeValue is null
           || permission.ScopeValue is null
           || string.Equals(permission.ScopeValue, scopeValue, StringComparison.Ordinal);

    private async Task<List<EffectivePermission>> ComputeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var assignments = await db.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);
        var active = assignments.Where(a => a.IsEffectiveAt(now)).ToList();
        if (active.Count == 0)
        {
            return [];
        }

        var graph = (await db.Roles.AsNoTracking()
                .Select(r => new { r.Id, r.ParentRoleIds })
                .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Id, r => (IReadOnlyList<Guid>)r.ParentRoleIds.ToList());

        var closures = active.Select(a => (Assignment: a, Closure: Closure(a.RoleId, graph))).ToList();
        var allRoleIds = closures.SelectMany(c => c.Closure).Distinct().ToHashSet();

        var permsByRole = (await db.RolePermissions.AsNoTracking()
                .Where(p => allRoleIds.Contains(p.RoleId))
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.RoleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<EffectivePermission>();
        var seen = new HashSet<string>();
        foreach (var (assignment, closure) in closures)
        {
            foreach (var roleId in closure)
            {
                if (!permsByRole.TryGetValue(roleId, out var permissions))
                {
                    continue;
                }

                foreach (var permission in permissions)
                {
                    var key = $"{permission.PermissionKey}|{permission.ScopeType}|{assignment.ScopeValue}";
                    if (seen.Add(key))
                    {
                        result.Add(new EffectivePermission(permission.PermissionKey, permission.ScopeType, assignment.ScopeValue, permission.ScopePredicateJson));
                    }
                }
            }
        }

        return result;
    }

    private static HashSet<Guid> Closure(Guid start, IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> graph)
    {
        var result = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current))
            {
                continue;
            }

            if (graph.TryGetValue(current, out var parents))
            {
                foreach (var parent in parents)
                {
                    stack.Push(parent);
                }
            }
        }

        return result;
    }

    private async Task<long> GetVersionAsync(IDatabase database)
    {
        var value = await database.StringGetAsync(VersionKey());
        return value.HasValue && long.TryParse(value.ToString(), out var version) ? version : 0;
    }

    private RedisKey VersionKey() => $"{_instance}perm:ver";

    private RedisKey UserKey(long version, Guid userId) => $"{_instance}perm:{version}:{userId}";
}
