using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class EvidenceRepository(AppDbContext db) : IEvidenceRepository
{
    public Task<EvidenceFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.EvidenceFiles.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EvidenceFile>> ListForContextAsync(Guid auditId, EvidenceContextType contextType, Guid contextId, CancellationToken cancellationToken = default)
        => await db.EvidenceFiles
            .Where(e => e.AuditId == auditId && e.ContextType == contextType && e.ContextId == contextId)
            .OrderBy(e => e.UploadedAt)
            .ToListAsync(cancellationToken);

    public async Task<long> SumSizeForAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.EvidenceFiles.Where(e => e.AuditId == auditId).SumAsync(e => e.SizeBytes, cancellationToken);

    public async Task<IReadOnlyList<EvidenceFile>> ListFlaggedAsync(CancellationToken cancellationToken = default)
        => await db.EvidenceFiles.AsNoTracking()
            .Where(e => e.IsFlagged)
            .OrderByDescending(e => e.UploadedAt)
            .ToListAsync(cancellationToken);

    public void Add(EvidenceFile evidence) => db.EvidenceFiles.Add(evidence);
}
