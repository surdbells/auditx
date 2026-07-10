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

    public async Task<CursorPage<AuditException>> SearchAsync(ExceptionSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(db.Exceptions.AsNoTracking(), filter);

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(e => e.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(e => e.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AuditException>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
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

    public void Add(AuditException exception) => db.Exceptions.Add(exception);
}
