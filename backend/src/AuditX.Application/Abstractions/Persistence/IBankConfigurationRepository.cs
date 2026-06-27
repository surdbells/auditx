using AuditX.Application.Common.Models;
using AuditX.Domain.Configuration;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence + lookup for the generic versioned bank-configuration store (M12).</summary>
public interface IBankConfigurationRepository
{
    /// <summary>The currently active version for a domain (tracked), or <c>null</c> if none.</summary>
    Task<BankConfiguration?> GetActiveAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>A specific (domain, version) row (tracked), or <c>null</c> if not found.</summary>
    Task<BankConfiguration?> GetByDomainVersionAsync(string domain, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>A version by id (tracked), or <c>null</c> if not found. Used by the maker-checker replay executor.</summary>
    Task<BankConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Highest version number assigned for a domain (0 if none), for computing the next draft number.</summary>
    Task<int> GetMaxVersionNumberAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Keyset-paginated version timeline for a domain, newest version first (includes the change reason).</summary>
    Task<CursorPage<BankConfiguration>> ListVersionsAsync(string domain, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the active flag on whichever version is currently active for a domain, as an immediate set-based UPDATE.
    /// Ordered BEFORE the tracked activate so the filtered unique index on <c>(domain, is_active=1)</c> can never
    /// momentarily see two active rows during a switch (the M7 grid HIGH-fix ordering).
    /// </summary>
    Task DeactivateActiveAsync(string domain, CancellationToken cancellationToken = default);

    void Add(BankConfiguration configuration);
}
