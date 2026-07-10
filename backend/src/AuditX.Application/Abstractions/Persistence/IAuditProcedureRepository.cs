using AuditX.Domain.Execution;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for <see cref="AuditProcedure"/> (P2-C typed execution procedures).</summary>
public interface IAuditProcedureRepository
{
    /// <summary>Loads a tracked procedure for mutation (soft-deleted rows are excluded by the global filter).</summary>
    Task<AuditProcedure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All live procedures for an audit, most-recent first.</summary>
    Task<IReadOnlyList<AuditProcedure>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default);

    void Add(AuditProcedure procedure);
}
