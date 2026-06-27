using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class SanctionsCaseRepository(AppDbContext db) : ISanctionsCaseRepository
{
    public Task<SanctionsCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.SanctionsCases.Include(c => c.TeamMembers).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CursorPage<SanctionsCase>> ListPagedAsync(SanctionsCaseStatus? status, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.SanctionsCases.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(c => c.Status == s);
        }

        return await PageAsync(query, page, cancellationToken);
    }

    public async Task<CursorPage<SanctionsCase>> ListReferredAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.SanctionsCases.AsNoTracking().Where(c => c.Status == SanctionsCaseStatus.DcReferral);
        return await PageAsync(query, page, cancellationToken);
    }

    public void Add(SanctionsCase sanctionsCase) => db.SanctionsCases.Add(sanctionsCase);

    private static async Task<CursorPage<SanctionsCase>> PageAsync(IQueryable<SanctionsCase> query, PageRequest page, CancellationToken cancellationToken)
    {
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

        return new CursorPage<SanctionsCase>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }
}

public sealed class SanctionsGridRepository(AppDbContext db) : ISanctionsGridRepository
{
    public Task DeactivateActiveAsync(CancellationToken cancellationToken = default)
        => db.SanctionsGridVersions
            .Where(g => g.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsActive, false), cancellationToken);

    public Task<SanctionsGridVersion?> GetActiveAsync(CancellationToken cancellationToken = default)
        => db.SanctionsGridVersions.FirstOrDefaultAsync(g => g.IsActive, cancellationToken);

    public Task<SanctionsGridVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.SanctionsGridVersions.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<int> GetMaxVersionNumberAsync(CancellationToken cancellationToken = default)
        => await db.SanctionsGridVersions.AnyAsync(cancellationToken)
            ? await db.SanctionsGridVersions.MaxAsync(g => g.VersionNumber, cancellationToken)
            : 0;

    public void Add(SanctionsGridVersion gridVersion) => db.SanctionsGridVersions.Add(gridVersion);
}

public sealed class SanctionsAppealRepository(AppDbContext db) : ISanctionsAppealRepository
{
    public Task<SanctionsAppeal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.SanctionsAppeals.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<SanctionsAppeal?> GetLatestByCaseAsync(Guid sanctionsCaseId, CancellationToken cancellationToken = default)
        => db.SanctionsAppeals.AsNoTracking()
            .Where(a => a.SanctionsCaseId == sanctionsCaseId)
            .OrderByDescending(a => a.FiledAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(SanctionsAppeal appeal) => db.SanctionsAppeals.Add(appeal);
}
