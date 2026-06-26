using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Abstractions.Persistence;

public interface IEvidenceRepository
{
    /// <summary>Loads a non-deleted evidence file (the soft-delete global filter excludes removed rows).</summary>
    Task<EvidenceFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvidenceFile>> ListForContextAsync(Guid auditId, EvidenceContextType contextType, Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Total bytes of non-deleted evidence for an audit, for the per-audit aggregate cap (US-M5-010).</summary>
    Task<long> SumSizeForAuditAsync(Guid auditId, CancellationToken cancellationToken = default);

    void Add(EvidenceFile evidence);
}
