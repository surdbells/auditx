using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.ReferenceData;
using AuditX.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ReferenceDataRepository(AppDbContext db) : IReferenceDataRepository
{
    public async Task<IReadOnlyList<ReferenceDataItem>> ListByCategoryAsync(string category, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.ReferenceDataItems.AsNoTracking().Include(i => i.LabelVersions).Where(i => i.Category == category);
        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.OrderBy(i => i.SortOrder).ThenBy(i => i.Label).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ReferenceDataCategoryCounts>> CountByCategoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.ReferenceDataItems.AsNoTracking()
            .GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Active = g.Count(i => i.IsActive), Inactive = g.Count(i => !i.IsActive) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Category, r => new ReferenceDataCategoryCounts(r.Active, r.Inactive), StringComparer.Ordinal);
    }

    public Task<ReferenceDataItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ReferenceDataItems.Include(i => i.LabelVersions).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string category, string code, CancellationToken cancellationToken = default)
        => db.ReferenceDataItems.AnyAsync(i => i.Category == category && i.Code == code, cancellationToken);

    public Task<ReferenceDataItem?> FindBySortOrderAsync(string category, int sortOrder, Guid? exceptId, CancellationToken cancellationToken = default)
        => db.ReferenceDataItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Category == category && i.SortOrder == sortOrder && (exceptId == null || i.Id != exceptId), cancellationToken);

    public void Add(ReferenceDataItem item) => db.ReferenceDataItems.Add(item);
}
