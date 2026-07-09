using AuditX.Domain.TimeTracking;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for <see cref="TimeEntry"/> (P0-B time/effort capture).</summary>
public interface ITimeEntryRepository
{
    /// <summary>Loads a tracked entry for mutation (soft-deleted rows are excluded by the global filter).</summary>
    Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All live entries for an audit, most-recent work first.</summary>
    Task<IReadOnlyList<TimeEntry>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default);

    void Add(TimeEntry entry);
}
