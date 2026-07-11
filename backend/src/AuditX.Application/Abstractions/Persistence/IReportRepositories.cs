using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;

namespace AuditX.Application.Abstractions.Persistence;

public interface IReportRepository
{
    /// <summary>Loads a report including its distribution log.</summary>
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Offset-paginated report versions for an audit, newest version first (ordered by version number desc).</summary>
    Task<PagedResult<Report>> ListByAuditAsync(Guid auditId, PageSpec page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offset-paginated standalone (cross-audit) reports, newest first. When <paramref name="kind"/> is supplied the
    /// results are scoped to that single standalone kind; otherwise all standalone kinds are returned.
    /// </summary>
    Task<PagedResult<Report>> ListStandaloneAsync(ReportKind? kind, PageSpec page, CancellationToken cancellationToken = default);

    /// <summary>Offset-paginated distribution log for a report, ordered by dispatch time.</summary>
    Task<PagedResult<ReportDistribution>> ListDistributionsAsync(Guid reportId, PageSpec page, CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned for the audit so far (0 if none), for computing the next version.</summary>
    Task<int> GetNextVersionAsync(Guid auditId, CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned for a standalone kind so far (0 if none), for the next per-kind version.</summary>
    Task<int> GetNextVersionForKindAsync(ReportKind kind, CancellationToken cancellationToken = default);

    /// <summary>Completed reports whose retention date has passed (TRACKED, for the retention-expiry job to expire).</summary>
    Task<IReadOnlyList<Report>> ListExpirableAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);

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
