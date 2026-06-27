using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions;

namespace AuditX.Application.Abstractions.Persistence;

public interface ISanctionsCaseRepository
{
    /// <summary>Loads a case including its stored team members.</summary>
    Task<SanctionsCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated tracker over all cases, optionally filtered by status.</summary>
    Task<CursorPage<SanctionsCase>> ListPagedAsync(SanctionsCaseStatus? status, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated queue of cases referred to the disciplinary committee (status <c>dc_referral</c>).</summary>
    Task<CursorPage<SanctionsCase>> ListReferredAsync(PageRequest page, CancellationToken cancellationToken = default);

    void Add(SanctionsCase sanctionsCase);
}

public interface ISanctionsGridRepository
{
    Task<SanctionsGridVersion?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the active flag on whichever grid version is currently active, as an immediate set-based UPDATE.
    /// Used before activating a new version so the deactivate is ordered BEFORE the activate and the filtered
    /// unique index on <c>is_active = 1</c> can never momentarily see two active rows.
    /// </summary>
    Task DeactivateActiveAsync(CancellationToken cancellationToken = default);

    Task<SanctionsGridVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned so far (0 if none), for computing the next draft number.</summary>
    Task<int> GetMaxVersionNumberAsync(CancellationToken cancellationToken = default);

    void Add(SanctionsGridVersion gridVersion);
}

public interface ISanctionsAppealRepository
{
    Task<SanctionsAppeal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The most recently filed appeal for a case (so the case detail can surface a decidable appeal id).</summary>
    Task<SanctionsAppeal?> GetLatestByCaseAsync(Guid sanctionsCaseId, CancellationToken cancellationToken = default);

    void Add(SanctionsAppeal appeal);
}
