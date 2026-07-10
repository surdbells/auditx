using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Configuration;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class BankConfigurationRepository(AppDbContext db) : IBankConfigurationRepository
{
    // No-tracking: the active row is only READ here (DTO projection + before/after trail capture). The activate/
    // rollback switch deactivates it via a set-based ExecuteUpdateAsync, never through the change tracker, so a
    // tracked copy here would go stale; keeping it untracked avoids that entirely.
    public Task<BankConfiguration?> GetActiveAsync(string domain, CancellationToken cancellationToken = default)
        => db.BankConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Domain == domain && c.IsActive, cancellationToken);

    public Task<BankConfiguration?> GetByDomainVersionAsync(string domain, int versionNumber, CancellationToken cancellationToken = default)
        => db.BankConfigurations.FirstOrDefaultAsync(c => c.Domain == domain && c.VersionNumber == versionNumber, cancellationToken);

    public Task<BankConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.BankConfigurations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<int> GetMaxVersionNumberAsync(string domain, CancellationToken cancellationToken = default)
        => await db.BankConfigurations.AnyAsync(c => c.Domain == domain, cancellationToken)
            ? await db.BankConfigurations.Where(c => c.Domain == domain).MaxAsync(c => c.VersionNumber, cancellationToken)
            : 0;

    public async Task<PagedResult<BankConfiguration>> ListVersionsAsync(string domain, PageSpec page, CancellationToken cancellationToken = default)
    {
        // Newest version first, ordered by the integer version_number (per-domain unique, monotonically increasing).
        var query = db.BankConfigurations.AsNoTracking().Where(c => c.Domain == domain);
        return await query.OrderByDescending(c => c.VersionNumber).ToPagedResultAsync(page, cancellationToken);
    }

    public Task DeactivateActiveAsync(string domain, CancellationToken cancellationToken = default)
        => db.BankConfigurations
            .Where(c => c.Domain == domain && c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), cancellationToken);

    public void Add(BankConfiguration configuration) => db.BankConfigurations.Add(configuration);
}
