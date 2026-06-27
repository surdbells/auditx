using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Analytics;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence.Naming;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(AppDbContext db) : IDashboardRepository
{
    public async Task<IReadOnlyList<Dashboard>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Dashboards.AsNoTracking()
            .Include(d => d.Widgets)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

    public Task<Dashboard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Dashboards.Include(d => d.Widgets).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Dashboard?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => db.Dashboards.Include(d => d.Widgets).FirstOrDefaultAsync(d => d.Slug == slug, cancellationToken);

    public void Add(Dashboard dashboard) => db.Dashboards.Add(dashboard);
}

public sealed class RecurrenceClusterRepository(AppDbContext db) : IRecurrenceClusterRepository
{
    public async Task<IReadOnlyList<RecurrenceGroup>> GetClosedRecurrenceGroupsAsync(
        int windowMonths, int minCount, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
    {
        var since = asOfUtc.AddMonths(-windowMonths);

        // CLOSED only (Cancelled excluded), with a non-null entity id, closed within the window. Group by
        // (entity, category) and keep groups with ≥ minCount members. SubjectUserId is never touched here —
        // this reads M6 exception facts only. Materialise the candidate rows then group in memory so the
        // grouped projection (including the member id list) is provider-agnostic.
        var candidates = await db.Exceptions.AsNoTracking()
            .Where(e => e.Status == ExceptionStatus.Closed
                && e.AuditableEntityId != null
                && e.ClosedAt != null
                && e.ClosedAt >= since)
            .Select(e => new { e.Id, EntityId = e.AuditableEntityId!.Value, e.Category, ClosedAt = e.ClosedAt!.Value })
            .ToListAsync(cancellationToken);

        return candidates
            .GroupBy(e => new { e.EntityId, Category = e.Category == null ? null : e.Category.Trim() })
            .Where(g => g.Count() >= minCount)
            .Select(g => new RecurrenceGroup(
                g.Key.EntityId,
                g.Key.Category,
                g.OrderBy(e => e.ClosedAt).Select(e => e.Id).ToArray(),
                g.Min(e => e.ClosedAt),
                g.Max(e => e.ClosedAt)))
            .ToArray();
    }

    public async Task<IReadOnlyList<RecurrenceCluster>> ListTrackedAsync(CancellationToken cancellationToken = default)
        => await db.RecurrenceClusters.ToListAsync(cancellationToken);

    public async Task<CursorPage<RecurrenceCluster>> ListPagedAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        // Only ACTIVE clusters (still at/above the threshold). The daily scan downgrades a cluster's count when its
        // members age out of the window; such retired rows are kept (for re-cross re-detection) but hidden here.
        var query = db.RecurrenceClusters.AsNoTracking()
            .Where(c => c.ClosedExceptionCount >= RecurrenceCluster.DetectionThreshold);
        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(c => c.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(c => c.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<RecurrenceCluster>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public Task<RecurrenceCluster?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.RecurrenceClusters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecurrenceMemberProjection>> GetMembersAsync(IReadOnlyList<Guid> exceptionIds, CancellationToken cancellationToken = default)
    {
        if (exceptionIds.Count == 0)
        {
            return [];
        }

        var idSet = exceptionIds.ToHashSet();
        var rows = await db.Exceptions.AsNoTracking()
            .Where(e => idSet.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.AuditId,
                e.Title,
                e.Severity,
                e.Status,
                e.RaisedAt,
                e.ClosedAt,
            })
            .ToListAsync(cancellationToken);

        // Preserve the cluster's stored member order (chronological by close time).
        var byId = rows.ToDictionary(r => r.Id);
        return exceptionIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Select(r => new RecurrenceMemberProjection(
                r.Id, r.AuditId, r.Title, SnakeCase.Convert(r.Severity.ToString()), SnakeCase.Convert(r.Status.ToString()), r.RaisedAt, r.ClosedAt))
            .ToArray();
    }

    public void Add(RecurrenceCluster cluster) => db.RecurrenceClusters.Add(cluster);
}
