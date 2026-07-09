using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    public Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Reports.Include(r => r.Distributions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<CursorPage<Report>> ListByAuditAsync(Guid auditId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Reports.AsNoTracking().Where(r => r.AuditId == auditId);

        // Newest version first; keyset on the descending version number using an encoded int cursor (per-audit
        // versions are a dense incrementing int, so this is a stable total order without a tiebreaker).
        if (!string.IsNullOrWhiteSpace(page.Cursor) && int.TryParse(page.Cursor, out var cursorVersion))
        {
            query = query.Where(r => r.VersionNumber < cursorVersion);
        }

        var items = await query.OrderByDescending(r => r.VersionNumber).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<Report>(items, hasMore ? items[^1].VersionNumber.ToString() : null, hasMore);
    }

    public async Task<CursorPage<Report>> ListStandaloneAsync(ReportKind? kind, PageRequest page, CancellationToken cancellationToken = default)
    {
        // Standalone reports have no audit; order newest first. Report ids are GUID v7 (time-ordered), so a keyset on
        // the descending id is a stable, chronological total order without a tiebreaker (per-kind versions are not).
        var query = db.Reports.AsNoTracking().Where(r => r.AuditId == null);
        if (kind is { } k)
        {
            query = query.Where(r => r.Kind == k);
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(r => r.Id.CompareTo(cursorId) < 0);
        }

        var items = await query.OrderByDescending(r => r.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<Report>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public async Task<CursorPage<ReportDistribution>> ListDistributionsAsync(Guid reportId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Set<ReportDistribution>().AsNoTracking().Where(d => d.ReportId == reportId);
        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(d => d.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(d => d.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<ReportDistribution>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public async Task<int> GetNextVersionAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.Reports.Where(r => r.AuditId == auditId).AnyAsync(cancellationToken)
            ? await db.Reports.Where(r => r.AuditId == auditId).MaxAsync(r => r.VersionNumber, cancellationToken)
            : 0;

    public async Task<int> GetNextVersionForKindAsync(ReportKind kind, CancellationToken cancellationToken = default)
        => await db.Reports.Where(r => r.AuditId == null && r.Kind == kind).AnyAsync(cancellationToken)
            ? await db.Reports.Where(r => r.AuditId == null && r.Kind == kind).MaxAsync(r => r.VersionNumber, cancellationToken)
            : 0;

    public void Add(Report report) => db.Reports.Add(report);
}

public sealed class ReportTemplateRepository(AppDbContext db) : IReportTemplateRepository
{
    public Task<ReportTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<ReportTemplate?> GetActiveAsync(CancellationToken cancellationToken = default)
        => db.ReportTemplates.FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

    public async Task<IReadOnlyList<ReportTemplate>> ListAsync(CancellationToken cancellationToken = default)
        => await db.ReportTemplates.AsNoTracking().OrderByDescending(t => t.VersionNumber).ToListAsync(cancellationToken);

    public async Task<int> GetMaxVersionNumberAsync(CancellationToken cancellationToken = default)
        => await db.ReportTemplates.AnyAsync(cancellationToken)
            ? await db.ReportTemplates.MaxAsync(t => t.VersionNumber, cancellationToken)
            : 0;

    public Task DeactivateActiveAsync(CancellationToken cancellationToken = default)
        => db.ReportTemplates
            .Where(t => t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), cancellationToken);

    public void Add(ReportTemplate template) => db.ReportTemplates.Add(template);
}
