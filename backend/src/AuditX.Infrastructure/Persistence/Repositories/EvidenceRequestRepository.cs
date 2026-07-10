using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class EvidenceRequestRepository(AppDbContext db) : IEvidenceRequestRepository
{
    public Task<EvidenceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.EvidenceRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EvidenceRequest>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.EvidenceRequests.AsNoTracking()
            .Where(r => r.AuditId == auditId)
            // Outstanding first (Requested = 0), then most-recently requested.
            .OrderBy(r => r.Status == EvidenceRequestStatus.Requested ? 0 : 1)
            .ThenByDescending(r => r.RequestedOn)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public void Add(EvidenceRequest request) => db.EvidenceRequests.Add(request);
}
