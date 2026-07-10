using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.SavedViews;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class SavedViewRepository(AppDbContext db) : ISavedViewRepository
{
    public Task<SavedView?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.SavedViews.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SavedView>> ListVisibleAsync(Guid userId, string viewKey, CancellationToken cancellationToken = default)
        => await db.SavedViews.AsNoTracking()
            .Where(v => v.ViewKey == viewKey && (v.OwnerUserId == userId || v.IsShared))
            // The caller's own views first, then shared views, each alphabetical by name.
            .OrderBy(v => v.OwnerUserId == userId ? 0 : 1)
            .ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> OwnedNameExistsAsync(Guid ownerUserId, string viewKey, string name, Guid? excludingId, CancellationToken cancellationToken = default)
        => db.SavedViews.AsNoTracking()
            .Where(v => v.OwnerUserId == ownerUserId && v.ViewKey == viewKey && v.Name == name)
            .Where(v => excludingId == null || v.Id != excludingId)
            .AnyAsync(cancellationToken);

    public void Add(SavedView view) => db.SavedViews.Add(view);
}
