using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Ac;
using AuditX.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class AcPackRepository(AppDbContext db) : IAcPackRepository
{
    public Task<AcPack?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.AcPacks.Include(p => p.Distributions).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<CursorPage<AcPack>> ListAsync(AcPackStatus? status, bool approvedOnly, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.AcPacks.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(p => p.Status == s);
        }

        // A non-CIA AC member only ever sees approved/distributed packs — apply BEFORE pagination so the page size and
        // cursor are computed over the visible set (a post-pagination filter would short the page and skew the cursor).
        if (approvedOnly)
        {
            query = query.Where(p => p.Status == AcPackStatus.Approved || p.Status == AcPackStatus.Distributed);
        }

        // Newest version first; keyset on the descending version number using an encoded int cursor.
        if (!string.IsNullOrWhiteSpace(page.Cursor) && int.TryParse(page.Cursor, out var cursorVersion))
        {
            query = query.Where(p => p.VersionNumber < cursorVersion);
        }

        var items = await query.OrderByDescending(p => p.VersionNumber).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AcPack>(items, hasMore ? items[^1].VersionNumber.ToString() : null, hasMore);
    }

    public async Task<CursorPage<AcPackDistribution>> ListDistributionsAsync(Guid acPackId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Set<AcPackDistribution>().AsNoTracking().Where(d => d.AcPackId == acPackId);
        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(d => d.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(d => d.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AcPackDistribution>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public async Task<int> GetMaxVersionNumberAsync(CancellationToken cancellationToken = default)
        => await db.AcPacks.AnyAsync(cancellationToken)
            ? await db.AcPacks.MaxAsync(p => p.VersionNumber, cancellationToken)
            : 0;

    public void Add(AcPack pack) => db.AcPacks.Add(pack);
}

public sealed class AcActionItemRepository(AppDbContext db) : IAcActionItemRepository
{
    public Task<AcActionItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.AcActionItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<CursorPage<AcActionItem>> ListAsync(AcActionItemStatus? status, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.AcActionItems.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(i => i.Status == s);
        }

        // Newest first; keyset on the descending GUIDv7 id (time-ordered).
        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(i => i.Id.CompareTo(cursorId) < 0);
        }

        var items = await query.OrderByDescending(i => i.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AcActionItem>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public void Add(AcActionItem item) => db.AcActionItems.Add(item);
}

public sealed class AcCommentRepository(AppDbContext db) : IAcCommentRepository
{
    public async Task<IReadOnlyList<AcComment>> ListByTargetAsync(AcCommentTargetType targetType, Guid targetId, CancellationToken cancellationToken = default)
        => await db.AcComments.AsNoTracking()
            .Where(c => c.TargetType == targetType && c.TargetId == targetId)
            .OrderBy(c => c.CommentedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public void Add(AcComment comment) => db.AcComments.Add(comment);
}

public sealed class FindingVisibilityRestrictionRepository(AppDbContext db) : IFindingVisibilityRestrictionRepository
{
    public Task<FindingVisibilityRestriction?> GetAsync(FindingType findingType, Guid findingId, CancellationToken cancellationToken = default)
        => db.FindingVisibilityRestrictions.FirstOrDefaultAsync(r => r.FindingType == findingType && r.FindingId == findingId, cancellationToken);

    public async Task<IReadOnlyList<FindingVisibilityRestriction>> ListByTypeAsync(FindingType findingType, CancellationToken cancellationToken = default)
        => await db.FindingVisibilityRestrictions.AsNoTracking()
            .Where(r => r.FindingType == findingType)
            .ToListAsync(cancellationToken);

    public void Add(FindingVisibilityRestriction restriction) => db.FindingVisibilityRestrictions.Add(restriction);
}
