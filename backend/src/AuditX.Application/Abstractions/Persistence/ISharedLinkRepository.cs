using AuditX.Domain.Enums;
using AuditX.Domain.Sharing;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for <see cref="SharedLink"/> (D3-B shareable links).</summary>
public interface ISharedLinkRepository
{
    Task<SharedLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves a link by its slug (soft-deleted rows are excluded by the global filter).</summary>
    Task<SharedLink?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>The links for a target, newest first (includes revoked/expired so a manager can audit them).</summary>
    Task<IReadOnlyList<SharedLink>> ListForTargetAsync(SharedLinkTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);

    void Add(SharedLink link);
}
