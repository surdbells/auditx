using AuditX.Domain.Evidence;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for <see cref="EvidenceRequest"/> (P2-D expected/requested evidence).</summary>
public interface IEvidenceRequestRepository
{
    /// <summary>Loads a tracked request for mutation (soft-deleted rows are excluded by the global filter).</summary>
    Task<EvidenceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All live evidence requests for an audit, outstanding first then most-recent.</summary>
    Task<IReadOnlyList<EvidenceRequest>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default);

    /// <summary>Live requests addressed to a given auditee (their upload worklist); outstanding first, then most-recent.</summary>
    Task<IReadOnlyList<EvidenceRequest>> ListByRequestedFromAsync(Guid requestedFromUserId, bool outstandingOnly, CancellationToken cancellationToken = default);

    void Add(EvidenceRequest request);
}
