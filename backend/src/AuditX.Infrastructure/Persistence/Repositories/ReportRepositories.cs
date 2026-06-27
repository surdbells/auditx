using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    public Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Reports.Include(r => r.Distributions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Report>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.Reports.AsNoTracking()
            .Where(r => r.AuditId == auditId)
            .OrderByDescending(r => r.VersionNumber)
            .ToListAsync(cancellationToken);

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
