using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Models;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class AuditRepository(AppDbContext db) : IAuditRepository
{
    public Task<Audit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Audits
            .Include(a => a.TeamMembers)
            .Include(a => a.Sections)
            .Include(a => a.ChecklistItems)
            .Include(a => a.Responses)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<CursorPage<Audit>> SearchAsync(
        AuditStatus? status, string? auditType, Guid? leadUserId, Guid? planItemId, string? search, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Audits.AsNoTracking().Include(a => a.ChecklistItems).AsQueryable();

        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.Name, $"%{term}%") ||
                (a.ScopeDescription != null && EF.Functions.Like(a.ScopeDescription, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(auditType))
        {
            query = query.Where(a => a.AuditType == auditType);
        }

        if (leadUserId is { } lead)
        {
            query = query.Where(a => a.LeadUserId == lead);
        }

        if (planItemId is { } planItem)
        {
            query = query.Where(a => a.PlanItemId == planItem);
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(a => a.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(a => a.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<Audit>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountsByStatusAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Audits.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Seed every status at zero so the dashboard map is always complete (no missing keys).
        var counts = Enum.GetValues<AuditStatus>().ToDictionary(s => s.ToSnake(), _ => 0);
        foreach (var row in rows)
        {
            counts[row.Status.ToSnake()] = row.Count;
        }

        return counts;
    }

    public async Task<IReadOnlyList<Audit>> GetPlannedDueToStartAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
        => await db.Audits
            .Where(a => a.Status == AuditStatus.Planned && a.StartDate <= asOfDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlanItemAuditProgress>> GetChecklistProgressByPlanItemIdsAsync(
        IReadOnlyCollection<Guid> planItemIds, CancellationToken cancellationToken = default)
    {
        if (planItemIds.Count == 0)
        {
            return [];
        }

        return await db.Audits.AsNoTracking()
            .Where(a => a.PlanItemId != null && planItemIds.Contains(a.PlanItemId.Value))
            .Select(a => new PlanItemAuditProgress(
                a.PlanItemId!.Value,
                a.Id,
                a.Status,
                a.ChecklistItems.Count,
                a.ChecklistItems.Count(i => i.ItemState == ChecklistItemState.Responded)))
            .ToListAsync(cancellationToken);
    }

    public void Add(Audit audit) => db.Audits.Add(audit);
}
