using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Execution;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class AuditProcedureRepository(AppDbContext db) : IAuditProcedureRepository
{
    public Task<AuditProcedure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.AuditProcedures.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AuditProcedure>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.AuditProcedures.AsNoTracking()
            .Where(p => p.AuditId == auditId)
            .OrderByDescending(p => p.PerformedOn)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public void Add(AuditProcedure procedure) => db.AuditProcedures.Add(procedure);
}
