using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ExceptionRepository(AppDbContext db) : IExceptionRepository
{
    public Task<AuditException?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Exceptions
            .Include(e => e.MapActions)
            .Include(e => e.Verifications)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AuditException>> ListByAuditAsync(Guid auditId, ExceptionStatus? status, CancellationToken cancellationToken = default)
    {
        var query = db.Exceptions.AsNoTracking().Where(e => e.AuditId == auditId);
        if (status is { } s)
        {
            query = query.Where(e => e.Status == s);
        }

        return await query.OrderBy(e => e.TargetDate).ThenBy(e => e.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditException>> ListByAuditWithMapActionsAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.Exceptions.AsNoTracking()
            .Include(e => e.MapActions)
            .Where(e => e.AuditId == auditId)
            .OrderBy(e => e.TargetDate).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Applies every <see cref="ExceptionSearchFilter"/> dimension (no pagination) — shared by search + export.</summary>
    private IQueryable<AuditException> ApplyFilter(IQueryable<AuditException> query, ExceptionSearchFilter filter)
    {
        // "Any open" umbrella (KPI drilldowns): open = everything except the two terminal states — the same
        // definition the analytics portfolio uses.
        if (filter.IsOpen is { } isOpen)
        {
            query = isOpen
                ? query.Where(e => e.Status != ExceptionStatus.Closed && e.Status != ExceptionStatus.Cancelled)
                : query.Where(e => e.Status == ExceptionStatus.Closed || e.Status == ExceptionStatus.Cancelled);
        }

        if (filter.Status is { } s)
        {
            query = query.Where(e => e.Status == s);
        }

        if (filter.Severity is { } sev)
        {
            query = query.Where(e => e.Severity == sev);
        }

        if (filter.OwnerUserId is { } owner)
        {
            query = query.Where(e => e.OwnerUserId == owner);
        }

        if (filter.AuditableEntityId is { } entity)
        {
            query = query.Where(e => e.AuditableEntityId == entity);
        }

        if (filter.AuditId is { } auditId)
        {
            query = query.Where(e => e.AuditId == auditId);
        }

        // Plan filter: no navigation properties link these aggregates, so correlate through
        // exception → audit (PlanItemId) → plan_item (AnnualPlanId) with an EXISTS subquery.
        if (filter.AnnualPlanId is { } planId)
        {
            query = query.Where(e => db.Audits.Any(a =>
                a.Id == e.AuditId && a.PlanItemId != null &&
                db.PlanItems.Any(p => p.Id == a.PlanItemId && p.AnnualPlanId == planId)));
        }

        if (filter.RaisedFrom is { } raisedFrom)
        {
            query = query.Where(e => e.RaisedAt >= raisedFrom);
        }

        if (filter.RaisedTo is { } raisedTo)
        {
            query = query.Where(e => e.RaisedAt <= raisedTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(e => e.Category == filter.Category);
        }

        if (!string.IsNullOrWhiteSpace(filter.RootCauseCategory))
        {
            // "uncategorised" is the analytics label for blanks — match null/blank so a drilldown on it works.
            query = filter.RootCauseCategory == "uncategorised"
                ? query.Where(e => e.RootCauseCategory == null || e.RootCauseCategory == "")
                : query.Where(e => e.RootCauseCategory == filter.RootCauseCategory);
        }

        if (!string.IsNullOrWhiteSpace(filter.NonConformanceCategory))
        {
            query = filter.NonConformanceCategory == "uncategorised"
                ? query.Where(e => e.NonConformanceCategory == null || e.NonConformanceCategory == "")
                : query.Where(e => e.NonConformanceCategory == filter.NonConformanceCategory);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(e =>
                e.Title.Contains(term) ||
                e.RootCause.Contains(term));
        }

        if (filter.IsRecurrence is { } rec)
        {
            query = query.Where(e => e.IsRecurrence == rec);
        }

        if (filter.IsOverdue is { } overdue)
        {
            query = overdue
                ? query.Where(e => e.Status != ExceptionStatus.Closed && e.Status != ExceptionStatus.Cancelled && e.TargetDate < filter.AsOfDate)
                : query.Where(e => e.Status == ExceptionStatus.Closed || e.Status == ExceptionStatus.Cancelled || e.TargetDate >= filter.AsOfDate);
        }

        return query;
    }

    public IAsyncEnumerable<ExceptionExportRow> StreamForExportAsync(ExceptionSearchFilter filter, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilter(db.Exceptions.AsNoTracking(), filter);
        return filtered
            .OrderByDescending(e => e.RaisedAt).ThenBy(e => e.Id)
            .Select(e => new ExceptionExportRow(
                e.Id, e.AuditId,
                db.Audits.Where(a => a.Id == e.AuditId).Select(a => a.Name).FirstOrDefault() ?? string.Empty,
                e.Title, e.Severity, e.Category, e.Status, e.CiaPending, e.IsRecurrence, e.OwnerUserId,
                e.AuditableEntityId, e.RaisedAt, e.TargetDate,
                e.MapActions.Count, e.MapActions.Count(a => a.Status == MapActionStatus.Complete),
                e.FinancialImpact, e.FinancialImpactCurrency))
            .AsAsyncEnumerable();
    }

    public async Task<PagedResult<AuditException>> SearchAsync(ExceptionSearchFilter filter, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(db.Exceptions.AsNoTracking(), filter);
        return await query.OrderBy(e => e.Id).ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<AuditException?> FindClosedForRecurrenceAsync(Guid auditableEntityId, string? category, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default)
    {
        var query = db.Exceptions.AsNoTracking()
            .Where(e => e.AuditableEntityId == auditableEntityId && e.Status == ExceptionStatus.Closed && e.ClosedAt >= sinceUtc);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(e => e.Category == category);
        }

        return await query.OrderByDescending(e => e.ClosedAt).FirstOrDefaultAsync(cancellationToken);
    }

    // Tracked (not AsNoTracking): the escalation job mutates each result and saves.
    public async Task<IReadOnlyList<AuditException>> ListOverdueForEscalationAsync(DateOnly today, CancellationToken cancellationToken = default)
        => await db.Exceptions
            .Where(e => e.Status == ExceptionStatus.Open
                && e.MapOverdueEscalatedAt == null
                && ((e.ManagementResponseDueDate != null && e.ManagementResponseDueDate < today)
                    || (e.ManagementResponseDueDate == null && e.TargetDate < today)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, OwnerFindingCounts>> CountOpenAndOverdueByOwnersAsync(
        IReadOnlyCollection<Guid> ownerIds, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (ownerIds.Count == 0)
        {
            return new Dictionary<Guid, OwnerFindingCounts>();
        }

        // Open = not closed/cancelled. Overdue computed in memory to avoid translating the due-date fallback.
        var rows = await db.Exceptions.AsNoTracking()
            .Where(e => ownerIds.Contains(e.OwnerUserId)
                && e.Status != ExceptionStatus.Closed && e.Status != ExceptionStatus.Cancelled)
            .Select(e => new { e.OwnerUserId, e.ManagementResponseDueDate, e.TargetDate })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.OwnerUserId)
            .ToDictionary(
                g => g.Key,
                g => new OwnerFindingCounts(g.Count(), g.Count(x => (x.ManagementResponseDueDate ?? x.TargetDate) < today)));
    }

    public void Add(AuditException exception) => db.Exceptions.Add(exception);
}

public sealed class ExceptionRaisingRuleRepository(AppDbContext db) : IExceptionRaisingRuleRepository
{
    public Task<ExceptionRaisingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ExceptionRaisingRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ExceptionRaisingRule?> GetByResponseTypeAsync(ResponseType responseType, CancellationToken cancellationToken = default)
        => db.ExceptionRaisingRules.AsNoTracking().FirstOrDefaultAsync(r => r.ResponseType == responseType, cancellationToken);

    public async Task<IReadOnlyList<ExceptionRaisingRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.ExceptionRaisingRules.AsNoTracking().OrderBy(r => r.ResponseType).ToListAsync(cancellationToken);

    public void Add(ExceptionRaisingRule rule) => db.ExceptionRaisingRules.Add(rule);
}

public sealed class RootCauseGapRepository(AppDbContext db) : IRootCauseGapRepository
{
    public Task<RootCauseGap?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.RootCauseGaps.Include(g => g.Links).Include(g => g.Remediations).FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<PagedResult<RootCauseGap>> SearchAsync(RootCauseGapStatus? status, string? search, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = db.RootCauseGaps.AsNoTracking().Include(g => g.Links).AsQueryable();
        if (status is { } s)
        {
            query = query.Where(g => g.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(g => g.Title.Contains(term) || (g.Description != null && g.Description.Contains(term)));
        }

        return await query.OrderByDescending(g => g.IdentifiedAt).ThenBy(g => g.Id).ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<RootCauseGapLinkedExceptionRow>> ListLinkedExceptionsAsync(Guid gapId, CancellationToken cancellationToken = default)
        => await db.RootCauseGapExceptionLinks.AsNoTracking()
            .Where(l => l.RootCauseGapId == gapId)
            .Join(db.Exceptions, l => l.ExceptionId, e => e.Id, (l, e) => new { l, e })
            .OrderBy(x => x.e.Title)
            .Select(x => new RootCauseGapLinkedExceptionRow(x.l.Id, x.e.Id, x.e.Title, x.e.Severity, x.e.Status))
            .ToListAsync(cancellationToken);

    public void Add(RootCauseGap gap) => db.RootCauseGaps.Add(gap);
}
