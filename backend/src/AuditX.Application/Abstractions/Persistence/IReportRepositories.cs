using AuditX.Application.Common.Models;
using AuditX.Domain.Reports;

namespace AuditX.Application.Abstractions.Persistence;

public interface IReportRepository
{
    /// <summary>Loads a report including its distribution log.</summary>
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated report versions for an audit, newest version first (keyset on version number desc).</summary>
    Task<CursorPage<Report>> ListByAuditAsync(Guid auditId, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated distribution log for a report, ordered by dispatch time.</summary>
    Task<CursorPage<ReportDistribution>> ListDistributionsAsync(Guid reportId, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned for the audit so far (0 if none), for computing the next version.</summary>
    Task<int> GetNextVersionAsync(Guid auditId, CancellationToken cancellationToken = default);

    void Add(Report report);
}

public interface IReportTemplateRepository
{
    Task<ReportTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReportTemplate?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportTemplate>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned so far (0 if none).</summary>
    Task<int> GetMaxVersionNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the active flag on whichever template is currently active, as an immediate set-based UPDATE. Used
    /// before activating a new version so the deactivate is ordered BEFORE the activate and the filtered unique
    /// index on <c>is_active = 1</c> can never momentarily see two active rows (the M7 grid HIGH-fix lesson).
    /// </summary>
    Task DeactivateActiveAsync(CancellationToken cancellationToken = default);

    void Add(ReportTemplate template);
}
