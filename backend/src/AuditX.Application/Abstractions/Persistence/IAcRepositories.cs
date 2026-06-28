using AuditX.Application.Common.Models;
using AuditX.Domain.Ac;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Persistence;

public interface IAcPackRepository
{
    /// <summary>Loads a pack including its distribution log.</summary>
    Task<AcPack?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keyset-paginated packs (newest version first), optionally filtered by status. When
    /// <paramref name="approvedOnly"/> is true (a non-CIA AC member), only approved/distributed packs are returned —
    /// filtered IN the query before pagination so the cursor + page size stay correct (a post-pagination filter would
    /// yield short/empty pages and a misaligned cursor).
    /// </summary>
    Task<CursorPage<AcPack>> ListAsync(AcPackStatus? status, bool approvedOnly, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated distribution log for a pack, ordered by dispatch time.</summary>
    Task<CursorPage<AcPackDistribution>> ListDistributionsAsync(Guid acPackId, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned bank-wide so far (0 if none), for computing the next version.</summary>
    Task<int> GetMaxVersionNumberAsync(CancellationToken cancellationToken = default);

    void Add(AcPack pack);
}

public interface IAcActionItemRepository
{
    Task<AcActionItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated action items (newest first), optionally filtered by status.</summary>
    Task<CursorPage<AcActionItem>> ListAsync(AcActionItemStatus? status, PageRequest page, CancellationToken cancellationToken = default);

    void Add(AcActionItem item);
}

public interface IAcCommentRepository
{
    /// <summary>All comments on one polymorphic target, oldest first.</summary>
    Task<IReadOnlyList<AcComment>> ListByTargetAsync(AcCommentTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);

    void Add(AcComment comment);
}

public interface IFindingVisibilityRestrictionRepository
{
    Task<FindingVisibilityRestriction?> GetAsync(FindingType findingType, Guid findingId, CancellationToken cancellationToken = default);

    /// <summary>All restrictions of a given finding type (for applying the per-requester filter across a pack/dashboard).</summary>
    Task<IReadOnlyList<FindingVisibilityRestriction>> ListByTypeAsync(FindingType findingType, CancellationToken cancellationToken = default);

    void Add(FindingVisibilityRestriction restriction);
}
