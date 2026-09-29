using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Configuration;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class InstitutionConfigurationRepository(AppDbContext db) : IInstitutionConfigurationRepository
{
    // No-tracking: the active row is only READ here (DTO projection + before/after trail capture). The activate/
    // rollback switch deactivates it via a set-based ExecuteUpdateAsync, never through the change tracker, so a
    // tracked copy here would go stale; keeping it untracked avoids that entirely.
    public Task<InstitutionConfiguration?> GetActiveAsync(string domain, CancellationToken cancellationToken = default)
        => db.InstitutionConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Domain == domain && c.IsActive, cancellationToken);

    public Task<InstitutionConfiguration?> GetByDomainVersionAsync(string domain, int versionNumber, CancellationToken cancellationToken = default)
        => db.InstitutionConfigurations.FirstOrDefaultAsync(c => c.Domain == domain && c.VersionNumber == versionNumber, cancellationToken);

    public Task<InstitutionConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.InstitutionConfigurations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<int> GetMaxVersionNumberAsync(string domain, CancellationToken cancellationToken = default)
        => await db.InstitutionConfigurations.AnyAsync(c => c.Domain == domain, cancellationToken)
            ? await db.InstitutionConfigurations.Where(c => c.Domain == domain).MaxAsync(c => c.VersionNumber, cancellationToken)
            : 0;

    public async Task<PagedResult<InstitutionConfiguration>> ListVersionsAsync(string domain, PageSpec page, CancellationToken cancellationToken = default)
    {
        // Newest version first, ordered by the integer version_number (per-domain unique, monotonically increasing).
        var query = db.InstitutionConfigurations.AsNoTracking().Where(c => c.Domain == domain);
        return await query.OrderByDescending(c => c.VersionNumber).ToPagedResultAsync(page, cancellationToken);
    }

    public Task DeactivateActiveAsync(string domain, CancellationToken cancellationToken = default)
        => db.InstitutionConfigurations
            .Where(c => c.Domain == domain && c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), cancellationToken);

    public void Add(InstitutionConfiguration configuration) => db.InstitutionConfigurations.Add(configuration);
}
