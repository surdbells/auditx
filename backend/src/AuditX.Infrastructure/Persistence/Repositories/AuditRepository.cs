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
            .Include(a => a.ReviewNotes)
            .Include(a => a.IndependenceDeclarations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<DateOnly?> GetLatestCompletionDateForEntityAsync(Guid auditableEntityId, Guid? excludingAuditId, CancellationToken cancellationToken = default)
        => await db.Audits.AsNoTracking()
            .Where(a => a.AuditableEntityId == auditableEntityId
                && a.Status == AuditStatus.Completed && !a.IsSelfAssessment && a.ActualEndDate != null
                && (excludingAuditId == null || a.Id != excludingAuditId))
            .MaxAsync(a => a.ActualEndDate, cancellationToken);

    public async Task<PagedResult<Audit>> SearchAsync(
        AuditStatus? status, string? auditType, Guid? leadUserId, Guid? planItemId, string? search, PageSpec page,
        Application.Abstractions.Authorization.ListScope? scope = null, CancellationToken cancellationToken = default)
    {
        var query = Within(db.Audits.AsNoTracking().Include(a => a.ChecklistItems).AsQueryable(), scope);

        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.Name.Contains(term) ||
                (a.ScopeDescription != null && a.ScopeDescription.Contains(term)));
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

        return await query.OrderBy(a => a.Id).ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<EngagementBoardAudit>> ListForEngagementBoardAsync(
        EngagementBoardReach reach, AuditStatus? status, int max, CancellationToken cancellationToken = default)
    {
        var query = db.Audits.AsNoTracking();
        if (!reach.AllAudits)
        {
            var uid = reach.UserId;
            var auditIds = reach.AuditIds.ToArray();
            var units = reach.OrgUnitIds.ToArray();
            query = query.Where(a => a.TeamMembers.Any(m => m.RemovedAt == null && m.UserId == uid)
                || auditIds.Contains(a.Id)
                || (a.AuditableEntityId != null && db.AuditUniverseEntities.IgnoreQueryFilters().Any(e =>
                    e.Id == a.AuditableEntityId && e.OrgUnitId != null && units.Contains(e.OrgUnitId.Value))));
        }

        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        var audits = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Take(max)
            .Include(a => a.TeamMembers)
            .Include(a => a.ChecklistItems)
            .Include(a => a.Responses)
            .Include(a => a.ReviewNotes)
            .Include(a => a.IndependenceDeclarations)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var entityIds = audits
            .Where(a => a.AuditableEntityId is not null)
            .Select(a => a.AuditableEntityId!.Value)
            .Distinct()
            .ToArray();
        Dictionary<Guid, Guid?> unitOf = entityIds.Length == 0
            ? []
            : await db.AuditUniverseEntities.IgnoreQueryFilters().AsNoTracking()
                .Where(e => entityIds.Contains(e.Id))
                .Select(e => new { e.Id, e.OrgUnitId })
                .ToDictionaryAsync(e => e.Id, e => e.OrgUnitId, cancellationToken);

        return [.. audits.Select(a => new EngagementBoardAudit(
            a, a.AuditableEntityId is { } entityId && unitOf.TryGetValue(entityId, out var unit) ? unit : null))];
    }

    /// <summary>
    /// Narrows audits to a caller whose permission is limited to some org units: those they are on (team or auditee)
    /// plus those whose entity belongs to one of the units. Null scope: no narrowing.
    /// </summary>
    private IQueryable<Audit> Within(IQueryable<Audit> query, Application.Abstractions.Authorization.ListScope? scope)
    {
        if (scope is null)
        {
            return query;
        }

        var uid = scope.UserId;
        var units = scope.OrgUnitIds.ToArray();
        return units.Length == 0
            ? query.Where(a => a.AuditeeUserId == uid || a.TeamMembers.Any(m => m.RemovedAt == null && m.UserId == uid))
            : query.Where(a => a.AuditeeUserId == uid
                || a.TeamMembers.Any(m => m.RemovedAt == null && m.UserId == uid)
                || (a.AuditableEntityId != null && db.AuditUniverseEntities.IgnoreQueryFilters().Any(e =>
                    e.Id == a.AuditableEntityId && e.OrgUnitId != null && units.Contains(e.OrgUnitId.Value))));
    }

    public async Task<IReadOnlyDictionary<string, int>> CountsByStatusAsync(
        Application.Abstractions.Authorization.ListScope? scope = null, CancellationToken cancellationToken = default)
    {
        var rows = await Within(db.Audits.AsNoTracking(), scope)
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
            // The team and its independence declarations decide whether an audit may start on its own.
            .Include(a => a.TeamMembers)
            .Include(a => a.IndependenceDeclarations)
            .AsSplitQuery()
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
            .Where(a => a.PlanItemId != null && a.AuditableEntityId != null && planItemIds.Contains(a.PlanItemId.Value))
            .Select(a => new PlanItemAuditProgress(
                a.PlanItemId!.Value,
                a.AuditableEntityId!.Value,
                a.Id,
                a.Status,
                a.ChecklistItems.Count,
                a.ChecklistItems.Count(i => i.ItemState == ChecklistItemState.Responded)))
            .ToListAsync(cancellationToken);
    }

    public void Add(Audit audit) => db.Audits.Add(audit);
}
