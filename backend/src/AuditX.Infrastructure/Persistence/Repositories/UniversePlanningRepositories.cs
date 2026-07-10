using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Planning;
using AuditX.Domain.Universe;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class AuditUniverseRepository(AppDbContext db) : IAuditUniverseRepository
{
    public Task<AuditableEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.AuditUniverseEntities.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<CursorPage<AuditableEntity>> SearchAsync(
        string? entityType, Guid? ownerUserId, bool includeArchived, string? search, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = (includeArchived ? db.AuditUniverseEntities.IgnoreQueryFilters() : db.AuditUniverseEntities).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(e => e.EntityType == entityType);
        }

        if (ownerUserId is { } owner)
        {
            query = query.Where(e => e.OwnerUserId == owner);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(e => e.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(e => e.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AuditableEntity>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetParentMapAsync(CancellationToken cancellationToken = default)
        // Only live entities may be referenced as parents or participate in cycle detection.
        => await db.AuditUniverseEntities.AsNoTracking()
            .Select(e => new { e.Id, e.ParentEntityId })
            .ToDictionaryAsync(e => e.Id, e => e.ParentEntityId, cancellationToken);

    public async Task<IReadOnlyList<AuditableEntity>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
        => names.Count == 0 ? [] : await db.AuditUniverseEntities.Where(e => names.Contains(e.Name)).ToListAsync(cancellationToken);

    public Task<bool> AnyOfTypeAsync(string entityType, CancellationToken cancellationToken = default)
        // Include archived entities so a type still referenced by soft-deleted rows cannot be removed.
        => db.AuditUniverseEntities.IgnoreQueryFilters().AnyAsync(e => e.EntityType == entityType, cancellationToken);

    public async Task<IReadOnlyList<AuditableEntity>> GetForCoverageAsync(string? entityType, CancellationToken cancellationToken = default)
    {
        var query = db.AuditUniverseEntities.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(e => e.EntityType == entityType);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public void Add(AuditableEntity entity) => db.AuditUniverseEntities.Add(entity);

    public void AddRange(IEnumerable<AuditableEntity> entities) => db.AuditUniverseEntities.AddRange(entities);
}

public sealed class RiskDimensionRepository(AppDbContext db) : IRiskDimensionRepository
{
    public Task<RiskDimension?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.RiskDimensions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<RiskDimension?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => db.RiskDimensions.FirstOrDefaultAsync(d => d.Name == name, cancellationToken);

    public async Task<IReadOnlyList<RiskDimension>> GetAllAsync(bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var query = db.RiskDimensions.AsQueryable();
        if (activeOnly is { } active)
        {
            query = query.Where(d => d.IsActive == active);
        }

        return await query.OrderBy(d => d.Name).ToListAsync(cancellationToken);
    }

    public void Add(RiskDimension dimension) => db.RiskDimensions.Add(dimension);
}

public sealed class EntityTypeTaxonomyRepository(AppDbContext db) : IEntityTypeTaxonomyRepository
{
    public async Task<IReadOnlyList<EntityTypeTaxonomy>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = db.EntityTypeTaxonomy.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public Task<EntityTypeTaxonomy?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => db.EntityTypeTaxonomy.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

    public void Add(EntityTypeTaxonomy taxonomy) => db.EntityTypeTaxonomy.Add(taxonomy);

    public void Remove(EntityTypeTaxonomy taxonomy) => db.EntityTypeTaxonomy.Remove(taxonomy);
}

public sealed class AnnualPlanRepository(AppDbContext db) : IAnnualPlanRepository
{
    public Task<AnnualPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.AnnualPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<AnnualPlan?> GetByPlanItemIdAsync(Guid planItemId, CancellationToken cancellationToken = default)
        => db.AnnualPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Items.Any(i => i.Id == planItemId), cancellationToken);

    public async Task<CursorPage<AnnualPlan>> SearchAsync(string? status, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.AnnualPlans.AsNoTracking().Include(p => p.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalised = status.Replace("_", string.Empty);
            if (Enum.TryParse<PlanStatus>(normalised, ignoreCase: true, out var parsed))
            {
                query = query.Where(p => p.Status == parsed);
            }
        }

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(p => p.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(p => p.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AnnualPlan>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public async Task<CursorPage<AnnualPlan>> SearchByLabelAsync(string term, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.AnnualPlans.AsNoTracking()
            .Where(p => p.PeriodLabel.Contains(term));

        if (!string.IsNullOrWhiteSpace(page.Cursor) && Guid.TryParse(page.Cursor, out var cursorId))
        {
            query = query.Where(p => p.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(p => p.Id).Take(page.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<AnnualPlan>(items, hasMore ? items[^1].Id.ToString() : null, hasMore);
    }

    public Task<bool> AnyOverlappingAsync(DateOnly periodStart, DateOnly periodEnd, Guid? excludePlanId, CancellationToken cancellationToken = default)
        => db.AnnualPlans.AnyAsync(
            p => (excludePlanId == null || p.Id != excludePlanId) && p.PeriodStart <= periodEnd && p.PeriodEnd >= periodStart,
            cancellationToken);

    public void Add(AnnualPlan plan) => db.AnnualPlans.Add(plan);
}
