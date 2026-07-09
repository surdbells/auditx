using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class OrgUnitRepository(AppDbContext db) : IOrgUnitRepository
{
    public Task<OrgUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.OrgUnits.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OrgUnit>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = db.OrgUnits.AsNoTracking().AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(o => !o.IsArchived);
        }

        return await query.OrderBy(o => o.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalised = code.Trim().ToUpperInvariant();
        return db.OrgUnits.AnyAsync(o => o.Code == normalised && (excludeId == null || o.Id != excludeId), cancellationToken);
    }

    public void Add(OrgUnit orgUnit) => db.OrgUnits.Add(orgUnit);
}
