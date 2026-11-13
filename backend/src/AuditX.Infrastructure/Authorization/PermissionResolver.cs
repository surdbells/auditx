using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Json;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Authorization;

/// <summary>
/// Resolves a user's effective permissions and caches them in Redis (5-minute TTL). They are the union of:
/// <list type="bullet">
///   <item>what their ROLES grant — role assignments, role inheritance and active delegations; and</item>
///   <item>the BASELINE (<see cref="BaselineAccess"/>) — the auditee abilities the platform gives automatically to
///         anyone it has named on something (an audit, a finding, an action, a document request, an area).</item>
/// </list>
/// Someone with no role who is named on nothing therefore has no permissions: that is the "Access pending" state.
/// A global version counter enables O(1) invalidation of every user's cache when a role definition changes; one
/// user's entry is dropped when their roles change or when they are newly named (see the save-changes interceptor).
/// </summary>
public sealed class PermissionResolver(
    AppDbContext db,
    PermissionCache cache,
    IClock clock)
    : IPermissionResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var database = cache.Database;
        var cacheKey = cache.UserKey(await cache.GetVersionAsync(), userId);

        var cached = await database.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            return AppJson.Deserialize<List<EffectivePermission>>(cached!);
        }

        var computed = await ComputeAsync(userId, cancellationToken);

        // "No access" is never cached: the moment such a user is granted a role or named on something, their very
        // next request must see it — not an empty answer remembered for five minutes.
        if (computed.Count > 0)
        {
            await database.StringSetAsync(cacheKey, AppJson.Serialize(computed), CacheTtl);
        }

        return computed;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionKey, string? scopeValue = null, CancellationToken cancellationToken = default)
    {
        var permissions = await GetEffectivePermissionsAsync(userId, cancellationToken);
        return permissions.Any(p => p.Key == permissionKey && Satisfies(p, scopeValue));
    }

    public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
        => cache.InvalidateUserAsync(userId);

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
        => await cache.Database.StringIncrementAsync(cache.VersionKey());

    private static bool Satisfies(EffectivePermission permission, string? scopeValue)
        => permission.ScopeType == PermissionScopeType.Global
           || scopeValue is null
           || permission.ScopeValue is null
           || string.Equals(permission.ScopeValue, scopeValue, StringComparison.Ordinal);

    private async Task<List<EffectivePermission>> ComputeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await ComputeFromRolesAsync(userId, cancellationToken);

        if (await HoldsBaselineAsync(userId, cancellationToken))
        {
            // Unscoped: each baseline permission is narrowed to the caller's involvement where it is used, so it
            // needs no scope value here. A key a role already grants unscoped is not repeated.
            foreach (var definition in BaselineAccess.Catalogue)
            {
                if (!result.Any(p => p.Key == definition.Key && p.ScopeValue is null))
                {
                    result.Add(new EffectivePermission(definition.Key, definition.FinestScope, null, null));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Whether the baseline applies: an account in good standing (not deactivated, not an ITANDT support login) that
    /// the platform has NAMED on something. Support accounts are time-boxed to the support role and nothing else.
    /// </summary>
    private async Task<bool> HoldsBaselineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, IsSupport = u.SupportChannelSessionId != null })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null || account.Status == UserStatus.Deactivated || account.IsSupport)
        {
            return false;
        }

        // Named = on an audit (auditee, lead or team), owner of a finding or of a remediation action, the person a
        // document was requested from, owner of an area, or answerable for a root-cause gap. Cheapest and likeliest
        // first; each is an indexed EXISTS and the chain stops at the first hit.
        return await db.Audits.AsNoTracking().AnyAsync(
                a => a.AuditeeUserId == userId || a.TeamMembers.Any(m => m.RemovedAt == null && m.UserId == userId), cancellationToken)
            || await db.Exceptions.AsNoTracking().AnyAsync(e => e.OwnerUserId == userId, cancellationToken)
            || await db.EvidenceRequests.AsNoTracking().AnyAsync(r => r.RequestedFromUserId == userId, cancellationToken)
            || await db.MapActions.AsNoTracking().AnyAsync(m => m.OwnerUserId == userId, cancellationToken)
            || await db.AuditUniverseEntities.AsNoTracking().AnyAsync(e => e.OwnerUserId == userId, cancellationToken)
            || await db.RootCauseGaps.AsNoTracking().AnyAsync(
                g => g.OwnerUserId == userId || g.Remediations.Any(r => r.OwnerUserId == userId), cancellationToken);
    }

    private async Task<List<EffectivePermission>> ComputeFromRolesAsync(Guid userId, CancellationToken cancellationToken)
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

}
