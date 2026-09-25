using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Identity;
using AuditX.Domain.Organization;

namespace AuditX.Application.Organization;

/// <summary>
/// Resolves a user's effective reporting line: the explicit <see cref="Domain.Identity.User.ManagerId"/> when set,
/// otherwise the head of the user's org unit — walking up the org-unit tree to the nearest ancestor whose head is
/// someone other than the user themselves. All walks are cycle-safe (visited set) and depth-capped, since the
/// manager/head references are soft (no FK) and could, in principle, form a loop.
/// </summary>
public interface IReportingLineResolver
{
    /// <summary>The effective line-manager user id for <paramref name="userId"/>, or null when none resolves.</summary>
    Task<Guid?> GetLineManagerAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The reporting chain above <paramref name="userId"/>, nearest manager first, excluding the user.</summary>
    Task<IReadOnlyList<Guid>> GetReportingChainAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The users whose effective manager is <paramref name="managerId"/> (their direct reports).</summary>
    Task<IReadOnlyList<Guid>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default);

    /// <summary>Every user below <paramref name="managerId"/> in the reporting tree (transitive reports, excluding the manager).</summary>
    Task<IReadOnlyList<Guid>> GetReportSubtreeAsync(Guid managerId, CancellationToken cancellationToken = default);
}

public sealed class ReportingLineResolver(IUserRepository users, IOrgUnitRepository orgUnits) : IReportingLineResolver
{
    private const int MaxDepth = 50;

    public async Task<Guid?> GetLineManagerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // 1) Explicit line manager, if set and still a live user (soft reference — the target may be soft-deleted).
        if (user.ManagerId is { } managerId && managerId != userId
            && await users.GetByIdAsync(managerId, cancellationToken) is not null)
        {
            return managerId;
        }

        // 2) Fallback: the head of the user's org unit, walking up ancestors until a head that isn't the user.
        return await ResolveOrgUnitHeadAsync(user.OrgUnitId, userId, cancellationToken);
    }

    private async Task<Guid?> ResolveOrgUnitHeadAsync(Guid? orgUnitId, Guid forUserId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var currentId = orgUnitId;
        var depth = 0;
        while (currentId is { } id && visited.Add(id) && depth++ < MaxDepth)
        {
            var unit = await orgUnits.GetByIdAsync(id, cancellationToken);
            if (unit is null)
            {
                break;
            }

            if (unit.HeadUserId is { } head && head != forUserId
                && await users.GetByIdAsync(head, cancellationToken) is not null)
            {
                return head;
            }

            currentId = unit.ParentOrgUnitId;
        }

        return null;
    }

    public async Task<IReadOnlyList<Guid>> GetReportingChainAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var chain = new List<Guid>();
        var visited = new HashSet<Guid> { userId };
        var currentId = userId;

        while (chain.Count < MaxDepth
            && await GetLineManagerAsync(currentId, cancellationToken) is { } managerId
            && visited.Add(managerId))
        {
            chain.Add(managerId);
            currentId = managerId;
        }

        return chain;
    }

    public async Task<IReadOnlyList<Guid>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        var effectiveManager = await BuildEffectiveManagerMapAsync(cancellationToken);
        return effectiveManager.Where(kv => kv.Value == managerId).Select(kv => kv.Key).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> GetReportSubtreeAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        var effectiveManager = await BuildEffectiveManagerMapAsync(cancellationToken);

        // Reverse the manager→? map into manager→[reports], then breadth-first down from managerId.
        var reports = new Dictionary<Guid, List<Guid>>();
        foreach (var (userId, mgr) in effectiveManager)
        {
            if (mgr is { } m)
            {
                (reports.TryGetValue(m, out var list) ? list : reports[m] = []).Add(userId);
            }
        }

        var subtree = new List<Guid>();
        var seen = new HashSet<Guid> { managerId };
        var queue = new Queue<Guid>();
        queue.Enqueue(managerId);
        while (queue.Count > 0)
        {
            if (!reports.TryGetValue(queue.Dequeue(), out var directReports))
            {
                continue;
            }

            foreach (var r in directReports)
            {
                if (seen.Add(r))
                {
                    subtree.Add(r);
                    queue.Enqueue(r);
                }
            }
        }

        return subtree;
    }

    /// <summary>Loads the whole user/org-unit graph once and computes each live user's effective manager in memory.</summary>
    private async Task<Dictionary<Guid, Guid?>> BuildEffectiveManagerMapAsync(CancellationToken cancellationToken)
    {
        var allUsers = await users.GetAllAsync(cancellationToken);
        var unitsById = (await orgUnits.GetAllAsync(includeArchived: true, cancellationToken)).ToDictionary(o => o.Id);
        var liveUserIds = allUsers.Select(u => u.Id).ToHashSet();

        var map = new Dictionary<Guid, Guid?>(allUsers.Count);
        foreach (var user in allUsers)
        {
            map[user.Id] = ComputeEffectiveManager(user, unitsById, liveUserIds);
        }

        return map;
    }

    private static Guid? ComputeEffectiveManager(User user, IReadOnlyDictionary<Guid, OrgUnit> unitsById, HashSet<Guid> liveUserIds)
    {
        if (user.ManagerId is { } managerId && managerId != user.Id && liveUserIds.Contains(managerId))
        {
            return managerId;
        }

        var visited = new HashSet<Guid>();
        var currentId = user.OrgUnitId;
        var depth = 0;
        while (currentId is { } id && visited.Add(id) && depth++ < MaxDepth)
        {
            if (!unitsById.TryGetValue(id, out var unit))
            {
                break;
            }

            if (unit.HeadUserId is { } head && head != user.Id && liveUserIds.Contains(head))
            {
                return head;
            }

            currentId = unit.ParentOrgUnitId;
        }

        return null;
    }
}
