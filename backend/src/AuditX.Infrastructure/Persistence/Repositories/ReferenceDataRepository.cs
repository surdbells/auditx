using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ReferenceDataRepository(AppDbContext db) : IReferenceDataRepository
{
    public async Task<IReadOnlyList<ReferenceDataItem>> ListByCategoryAsync(string category, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.ReferenceDataItems.AsNoTracking().Where(i => i.Category == category);
        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.OrderBy(i => i.SortOrder).ThenBy(i => i.Label).ToListAsync(cancellationToken);
    }

    public Task<ReferenceDataItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ReferenceDataItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string category, string code, CancellationToken cancellationToken = default)
        => db.ReferenceDataItems.AnyAsync(i => i.Category == category && i.Code == code, cancellationToken);

    public void Add(ReferenceDataItem item) => db.ReferenceDataItems.Add(item);
}
