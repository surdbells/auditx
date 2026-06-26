using AuditX.Application.Common.Models;
using AuditX.Domain.Planning;
using AuditX.Domain.Universe;

namespace AuditX.Application.Abstractions.Persistence;

public interface IAuditUniverseRepository
{
    Task<AuditableEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CursorPage<AuditableEntity>> SearchAsync(
        string? entityType, Guid? ownerUserId, bool includeArchived, string? search, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Whole-universe (id → parentId) map for in-memory acyclicity checks.</summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> GetParentMapAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditableEntity>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default);

    Task<bool> AnyOfTypeAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>Scored, non-deleted entities for coverage/heat-map analytics.</summary>
    Task<IReadOnlyList<AuditableEntity>> GetForCoverageAsync(string? entityType, CancellationToken cancellationToken = default);

    void Add(AuditableEntity entity);

    void AddRange(IEnumerable<AuditableEntity> entities);
}

public interface IRiskDimensionRepository
{
    Task<RiskDimension?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RiskDimension?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RiskDimension>> GetAllAsync(bool? activeOnly, CancellationToken cancellationToken = default);

    void Add(RiskDimension dimension);
}

public interface IEntityTypeTaxonomyRepository
{
    Task<IReadOnlyList<EntityTypeTaxonomy>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<EntityTypeTaxonomy?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    void Add(EntityTypeTaxonomy taxonomy);

    void Remove(EntityTypeTaxonomy taxonomy);
}

public interface IAnnualPlanRepository
{
    Task<AnnualPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Load the plan that owns a given plan item (M4 completion wiring → M3).</summary>
    Task<AnnualPlan?> GetByPlanItemIdAsync(Guid planItemId, CancellationToken cancellationToken = default);

    Task<CursorPage<AnnualPlan>> SearchAsync(string? status, PageRequest page, CancellationToken cancellationToken = default);

    Task<bool> AnyOverlappingAsync(DateOnly periodStart, DateOnly periodEnd, Guid? excludePlanId, CancellationToken cancellationToken = default);

    void Add(AnnualPlan plan);
}
