using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Universe;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Universe;

/// <summary>Reads the bank's active entity-type taxonomy and the audit types in use (for analytics axes).</summary>
public sealed class TaxonomyProvider(AppDbContext db) : ITaxonomyProvider
{
    public async Task<IReadOnlyList<string>> GetActiveEntityTypesAsync(CancellationToken cancellationToken = default)
        => await db.EntityTypeTaxonomy.Where(t => t.IsActive).Select(t => t.Name).OrderBy(n => n).ToListAsync(cancellationToken);

    public Task<bool> IsEntityTypeActiveAsync(string entityType, CancellationToken cancellationToken = default)
        => db.EntityTypeTaxonomy.AnyAsync(t => t.IsActive && t.Name == entityType, cancellationToken);

    public async Task<IReadOnlyList<string>> GetAuditTypesAsync(CancellationToken cancellationToken = default)
    {
        var fromTemplates = await db.Templates.Select(t => t.AuditType).Distinct().ToListAsync(cancellationToken);
        var fromPlanItems = await db.Set<Domain.Planning.PlanItem>().Select(i => i.AuditType).Distinct().ToListAsync(cancellationToken);
        return fromTemplates.Concat(fromPlanItems).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();
    }
}

/// <summary>
/// Coverage analytics read model (US-M3-021..023). Computes over the universe; audit-fact joins arrive
/// with M4 (until then the coverage matrix is zero-filled and last-audited reflects M3 state).
/// </summary>
public sealed class CoverageQueryService(AppDbContext db, IClock clock, ITaxonomyProvider taxonomy) : ICoverageQueryService
{
    public async Task<IReadOnlyList<NotAuditedRow>> NotAuditedSinceAsync(int months, string? entityType, CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow.AddMonths(-months);
        var query = db.AuditUniverseEntities.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(e => e.EntityType == entityType);
        }

        query = query.Where(e => e.LastAuditedAt == null || e.LastAuditedAt < cutoff);
        return await query
            .OrderBy(e => e.EntityType).ThenBy(e => e.Name)
            .Select(e => new NotAuditedRow(e.Id, e.EntityType, e.Name, e.LastAuditedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HighRiskGapRow>> HighRiskGapsAsync(int months, string? entityType, CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow.AddMonths(-months);
        var query = db.AuditUniverseEntities.AsNoTracking().Where(e => e.CompositeResidualScore != null);
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(e => e.EntityType == entityType);
        }

        var scored = await query
            .OrderByDescending(e => e.CompositeResidualScore)
            .ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.EntityType, e.Name, e.CompositeResidualScore, e.LastAuditedAt })
            .ToListAsync(cancellationToken);

        if (scored.Count == 0)
        {
            return [];
        }

        var topQuartileCount = (scored.Count + 3) / 4; // NTILE(4) group 1 size (remainder to earlier groups)
        return scored
            .Take(topQuartileCount)
            .Where(e => e.LastAuditedAt == null || e.LastAuditedAt < cutoff)
            .Select(e => new HighRiskGapRow(e.Id, e.EntityType, e.Name, e.CompositeResidualScore, e.LastAuditedAt))
            .ToArray();
    }

    public async Task<CoverageMatrix> CoverageMatrixAsync(int windowMonths, CancellationToken cancellationToken = default)
    {
        var rows = await taxonomy.GetActiveEntityTypesAsync(cancellationToken);
        var columns = await taxonomy.GetAuditTypesAsync(cancellationToken);

        // Cells = completed audits per (entity_type, audit_type) within the window. The audit fact table
        // arrives with M4; until then the matrix is zero-filled over the bounded, ordered taxonomy axes.
        var cells = new int[rows.Count][];
        for (var r = 0; r < rows.Count; r++)
        {
            cells[r] = new int[columns.Count];
        }

        return new CoverageMatrix(rows, columns, cells);
    }
}

/// <summary>Reads the append-only audit trail for per-object history surfaces (M11 core).</summary>
public sealed class AuditTrailReader(AppDbContext db) : IAuditTrailReader
{
    public async Task<IReadOnlyList<AuditTrailEntryView>> GetForTargetAsync(
        string targetObjectType, Guid targetObjectId, string? eventType, int limit, CancellationToken cancellationToken = default)
    {
        var query = db.AuditTrail.AsNoTracking()
            .Where(e => e.TargetObjectType == targetObjectType && e.TargetObjectId == targetObjectId);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        return await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(limit)
            .Select(e => new AuditTrailEntryView(
                e.Id, e.EventType, e.TargetObjectType, e.TargetObjectId, e.ActorUserId, e.ActorType.ToString(),
                e.OccurredAtUtc, e.BeforeStateJson, e.AfterStateJson, e.EventPayloadJson))
            .ToListAsync(cancellationToken);
    }
}
