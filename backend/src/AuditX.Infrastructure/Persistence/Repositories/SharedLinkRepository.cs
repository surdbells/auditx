using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Enums;
using AuditX.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class SharedLinkRepository(AppDbContext db) : ISharedLinkRepository
{
    public Task<SharedLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.SharedLinks.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<SharedLink?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => db.SharedLinks.FirstOrDefaultAsync(l => l.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<SharedLink>> ListForTargetAsync(
        SharedLinkTargetType targetType, Guid targetId, CancellationToken cancellationToken = default)
        => await db.SharedLinks.AsNoTracking()
            .Where(l => l.TargetType == targetType && l.TargetId == targetId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

    public void Add(SharedLink link) => db.SharedLinks.Add(link);
}
