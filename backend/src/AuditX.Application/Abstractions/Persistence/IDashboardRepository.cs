using AuditX.Domain.Analytics;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for M9 dashboards (aggregate root over its widgets).</summary>
public interface IDashboardRepository
{
    Task<IReadOnlyList<Dashboard>> ListAsync(CancellationToken cancellationToken = default);

    Task<Dashboard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Dashboard?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    void Add(Dashboard dashboard);
}
