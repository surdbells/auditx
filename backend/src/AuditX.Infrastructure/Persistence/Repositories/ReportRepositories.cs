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

    public async Task<PagedResult<Report>> ListByAuditAsync(Guid auditId, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = db.Reports.AsNoTracking().Where(r => r.AuditId == auditId);

        // Newest version first (per-audit versions are a dense incrementing int, a stable total order).
        return await query.OrderByDescending(r => r.VersionNumber).ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<PagedResult<Report>> ListStandaloneAsync(ReportKind? kind, PageSpec page, CancellationToken cancellationToken = default)
    {
        // Standalone reports have no audit; order newest first. Report ids are GUID v7 (time-ordered), so ordering by
        // the descending id is a stable, chronological total order.
        var query = db.Reports.AsNoTracking().Where(r => r.AuditId == null);
        if (kind is { } k)
        {
            query = query.Where(r => r.Kind == k);
        }

        return await query.OrderByDescending(r => r.Id).ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<PagedResult<ReportDistribution>> ListDistributionsAsync(Guid reportId, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = db.Set<ReportDistribution>().AsNoTracking().Where(d => d.ReportId == reportId);
        return await query.OrderBy(d => d.Id).ToPagedResultAsync(page, cancellationToken);
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
