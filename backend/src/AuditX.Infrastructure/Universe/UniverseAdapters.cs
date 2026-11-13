using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Universe;
using AuditX.Application.Common.Models;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Universe;

/// <summary>Reads the bank's active entity-type taxonomy and the audit types in use (for analytics axes).</summary>
public sealed class TaxonomyProvider(AppDbContext db) : ITaxonomyProvider
{
    // Entity types are a managed reference-data list (category "entity_type"): the same generic store, admin CRUD
    // screen and lazy dropdown lookup that back audit types and exception categories. The item CODE is the value
    // stored on auditable_entity.entity_type, so codes (not labels) drive validation, dropdowns and coverage rows.
    public async Task<IReadOnlyList<string>> GetActiveEntityTypesAsync(CancellationToken cancellationToken = default)
        => await db.ReferenceDataItems
            .Where(t => t.Category == Domain.ReferenceData.ReferenceDataCategories.EntityType && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> IsEntityTypeActiveAsync(string entityType, CancellationToken cancellationToken = default)
        => db.ReferenceDataItems.AnyAsync(
            t => t.Category == Domain.ReferenceData.ReferenceDataCategories.EntityType && t.IsActive && t.Code == entityType,
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetAuditTypesAsync(CancellationToken cancellationToken = default)
    {
        var fromTemplates = await db.Templates.Select(t => t.AuditType).Distinct().ToListAsync(cancellationToken);
        var fromPlanItems = await db.Set<Domain.Planning.PlanItem>().Select(i => i.AuditType).Distinct().ToListAsync(cancellationToken);
        // Audits themselves: an ad-hoc audit can use an audit type no template or plan item carries, and its completed
        // audit must still land on the coverage matrix rather than fall off the grid.
        var fromAudits = await db.Audits.Select(a => a.AuditType).Distinct().ToListAsync(cancellationToken);
        return fromTemplates.Concat(fromPlanItems).Concat(fromAudits)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();
    }
}

/// <summary>
/// Coverage analytics read model (US-M3-021..023). Computes over the universe and the M4 audit fact table:
/// the coverage matrix counts completed audits per (entity_type, audit_type) and last-audited reflects the
/// MarkAudited timestamp set on audit completion.
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

        var cells = new int[rows.Count][];
        for (var r = 0; r < rows.Count; r++)
        {
            cells[r] = new int[columns.Count];
        }

        // Cells = COMPLETED audits per (audited entity's entity_type, audit's audit_type) within the window.
        // An audit carries its audited entity directly (audit.auditable_entity_id → auditable_entity.entity_type):
        // the plan item's chosen entity for a plan-launched audit, or the entity picked for an ad-hoc one. Only an
        // ad-hoc audit left unlinked is not attributable to an entity_type row. The window is keyed on
        // actual_end_date (set on completion), expressed as a DateOnly cutoff to match the column type.
        var cutoff = DateOnly.FromDateTime(clock.UtcNow.AddMonths(-windowMonths).UtcDateTime);
        var counts = await (
            from audit in db.Audits.AsNoTracking()
            where audit.Status == Domain.Enums.AuditStatus.Completed
                && audit.ActualEndDate != null && audit.ActualEndDate >= cutoff
                && audit.AuditableEntityId != null
                && !audit.IsSelfAssessment // a self-assessment is not independent audit coverage
            join entity in db.AuditUniverseEntities.AsNoTracking() on audit.AuditableEntityId equals entity.Id
            group audit by new { entity.EntityType, audit.AuditType } into g
            select new { g.Key.EntityType, g.Key.AuditType, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var rowIndex = rows
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
        var columnIndex = columns
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        foreach (var count in counts)
        {
            // Only place counts whose entity_type and audit_type are on the (active, bounded) axes; an audit of
            // an entity whose type was since deactivated, or of an audit_type no longer in use, falls off-grid.
            if (rowIndex.TryGetValue(count.EntityType, out var r) && columnIndex.TryGetValue(count.AuditType, out var c))
            {
                cells[r][c] = count.Count;
            }
        }

        return new CoverageMatrix(rows, columns, cells);
    }

    public async Task<EntityAuditHistory?> EntityAuditHistoryAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        // Archived entities keep their history, so the soft-delete filter is lifted for the header lookup.
        var entity = await db.AuditUniverseEntities.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Id == entityId)
            .Select(e => new { e.Id, e.Name, e.EntityType, e.IsDeleted, e.LastAuditedAt, e.ExpectedAuditsPerYear })
            .FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var audits = await db.Audits.AsNoTracking()
            .Where(a => a.AuditableEntityId == entityId)
            .OrderByDescending(a => a.StartDate).ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id, a.Name, a.AuditType, a.Status, a.IsSelfAssessment, a.StartDate, a.TargetEndDate, a.ActualEndDate,
                a.LeadUserId, a.AuditeeUserId, a.PlanItemId,
                ChecklistItems = a.ChecklistItems.Count(),
                Responded = a.ChecklistItems.Count(i => i.ItemState == Domain.Enums.ChecklistItemState.Responded),
                Passed = a.Responses.Count(r => !r.IsDraft && r.Verdict == Domain.Enums.ResponseVerdict.Pass),
                Failed = a.Responses.Count(r => !r.IsDraft && r.Verdict == Domain.Enums.ResponseVerdict.Fail),
                NotApplicable = a.Responses.Count(r => !r.IsDraft && r.Verdict == Domain.Enums.ResponseVerdict.Na),
            })
            .ToListAsync(cancellationToken);

        var auditIds = audits.Select(a => a.Id).ToArray();

        // Findings and issued reports per audit. One entity's history is small, so these are folded in memory.
        var findings = await db.Exceptions.AsNoTracking()
            .Where(e => auditIds.Contains(e.AuditId))
            .Select(e => new { e.AuditId, e.Severity, e.Status })
            .ToListAsync(cancellationToken);
        var findingsByAudit = findings.ToLookup(f => f.AuditId);

        var reportsByAudit = (await db.Reports.AsNoTracking()
                .Where(r => r.AuditId != null && auditIds.Contains(r.AuditId.Value) && r.Status == Domain.Enums.ReportStatus.Completed)
                .GroupBy(r => r.AuditId!.Value)
                .Select(g => new { AuditId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.AuditId, x => x.Count);

        var userIds = audits.SelectMany(a => new[] { a.LeadUserId, a.AuditeeUserId }).Distinct().ToArray();
        var userNames = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var planItemIds = audits.Where(a => a.PlanItemId != null).Select(a => a.PlanItemId!.Value).Distinct().ToArray();
        var planLabels = planItemIds.Length == 0
            ? []
            : await db.AnnualPlans.AsNoTracking()
                .SelectMany(p => p.Items.Select(i => new { ItemId = i.Id, p.PeriodLabel }))
                .Where(x => planItemIds.Contains(x.ItemId))
                .ToDictionaryAsync(x => x.ItemId, x => x.PeriodLabel, cancellationToken);

        static bool IsOpen(Domain.Enums.ExceptionStatus status)
            => status != Domain.Enums.ExceptionStatus.Closed && status != Domain.Enums.ExceptionStatus.Cancelled;

        var rows = audits.Select(a =>
        {
            // Cancelled findings were withdrawn — they are not part of the audit's outcome.
            var raised = findingsByAudit[a.Id].Where(f => f.Status != Domain.Enums.ExceptionStatus.Cancelled).ToArray();
            return new EntityAuditHistoryRow(
                a.Id, a.Name, a.AuditType, a.Status, a.IsSelfAssessment, a.StartDate, a.TargetEndDate, a.ActualEndDate,
                a.LeadUserId, userNames.GetValueOrDefault(a.LeadUserId), a.AuditeeUserId, userNames.GetValueOrDefault(a.AuditeeUserId),
                a.PlanItemId, a.PlanItemId is { } itemId ? planLabels.GetValueOrDefault(itemId) : null,
                a.ChecklistItems, a.Responded, a.Passed, a.Failed, a.NotApplicable,
                raised.Length, raised.Count(f => IsOpen(f.Status)),
                raised.Count(f => f.Severity == Domain.Enums.ExceptionSeverity.Critical),
                raised.Count(f => f.Severity == Domain.Enums.ExceptionSeverity.High),
                raised.Count(f => f.Severity == Domain.Enums.ExceptionSeverity.Medium),
                raised.Count(f => f.Severity == Domain.Enums.ExceptionSeverity.Low),
                reportsByAudit.GetValueOrDefault(a.Id));
        }).ToArray();

        return new EntityAuditHistory(
            entity.Id, entity.Name, entity.EntityType, entity.IsDeleted, entity.LastAuditedAt, entity.ExpectedAuditsPerYear, rows);
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
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Take(limit)
            .Select(Projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditTrailFacets> GetFacetsAsync(CancellationToken cancellationToken = default)
    {
        var eventTypes = await db.AuditTrail.AsNoTracking()
            .Select(e => e.EventType).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var targetTypes = await db.AuditTrail.AsNoTracking()
            .Select(e => e.TargetObjectType).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        return new AuditTrailFacets(eventTypes, targetTypes);
    }

    public async Task<PagedResult<AuditTrailEntryView>> QueryAsync(AuditTrailFilter filter, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = Filtered(filter);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);
        return new PagedResult<AuditTrailEntryView>(rows, total, page.Page, page.PageSize);
    }

    public IAsyncEnumerable<AuditTrailEntryView> StreamAsync(AuditTrailFilter filter, CancellationToken cancellationToken = default)
        => Filtered(filter)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Select(Projection)
            .AsAsyncEnumerable();

    private IQueryable<Domain.AuditTrail.AuditTrailEntry> Filtered(AuditTrailFilter filter)
    {
        var query = db.AuditTrail.AsNoTracking();
        if (filter.ActorUserId is { } actor)
        {
            query = query.Where(e => e.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            query = query.Where(e => e.EventType == filter.EventType);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetObjectType))
        {
            query = query.Where(e => e.TargetObjectType == filter.TargetObjectType);
        }

        if (filter.TargetObjectId is { } targetId)
        {
            query = query.Where(e => e.TargetObjectId == targetId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(e => e.OccurredAtUtc >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(e => e.OccurredAtUtc <= to);
        }

        return query;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Domain.AuditTrail.AuditTrailEntry, AuditTrailEntryView>> Projection =
        e => new AuditTrailEntryView(
            e.Id, e.EventType, e.TargetObjectType, e.TargetObjectId, e.ActorUserId, e.ActorType.ToString(),
            e.ActorSystemLabel, e.OccurredAtUtc, e.OriginatingTimezone, e.BeforeStateJson, e.AfterStateJson,
            e.RequestContextJson, e.EventPayloadJson);
}
