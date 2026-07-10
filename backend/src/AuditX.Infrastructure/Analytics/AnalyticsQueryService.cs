using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Common.Enums;
using AuditX.Application.Risks;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Analytics;

/// <summary>
/// M9 KPI read model. Efficient aggregate LINQ over the AppDbContext read models. CRITICAL: the sanctions
/// projection NEVER selects <c>subject_user_id</c> (FR-M7-010 / NFR-SEC-007) — sanctions are aggregated by
/// business unit (category) only. Read-only: no audit-trail side effects. Computed live (Redis caching deferred).
/// </summary>
public sealed class AnalyticsQueryService(AppDbContext db, IClock clock) : IAnalyticsQueryService
{
    private static readonly ExceptionStatus[] OpenStatuses =
    [
        ExceptionStatus.Open, ExceptionStatus.MapSubmitted, ExceptionStatus.MapApproved,
        ExceptionStatus.MapRejected, ExceptionStatus.PendingClosure,
    ];

    public async Task<FunctionPerformanceDto> FunctionPerformanceAsync(CancellationToken cancellationToken = default)
    {
        var auditsInFlight = await db.Audits.AsNoTracking()
            .CountAsync(a => a.Status == AuditStatus.Planned || a.Status == AuditStatus.InProgress || a.Status == AuditStatus.UnderReview, cancellationToken);
        var auditsCompleted = await db.Audits.AsNoTracking().CountAsync(a => a.Status == AuditStatus.Completed, cancellationToken);

        var planItemsTotal = await db.PlanItems.AsNoTracking().CountAsync(cancellationToken);
        var planItemsCompleted = await db.PlanItems.AsNoTracking().CountAsync(i => i.Status == PlanItemStatus.Completed, cancellationToken);

        var openBacklog = await db.Exceptions.AsNoTracking().CountAsync(e => OpenStatuses.Contains(e.Status), cancellationToken);
        var closed = await db.Exceptions.AsNoTracking().CountAsync(e => e.Status == ExceptionStatus.Closed, cancellationToken);

        var planExecution = Percent(planItemsCompleted, planItemsTotal);
        var closureRate = Percent(closed, closed + openBacklog);

        return new FunctionPerformanceDto(
            auditsInFlight, auditsCompleted, planItemsTotal, planItemsCompleted, planExecution, openBacklog, closed, closureRate);
    }

    public async Task<ExceptionPortfolioDto> ExceptionPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var open = await db.Exceptions.AsNoTracking()
            .Where(e => OpenStatuses.Contains(e.Status))
            .Select(e => new { e.Id, e.Severity, e.RaisedAt, e.AuditableEntityId, e.RootCauseCategory })
            .ToListAsync(cancellationToken);

        var bySeverity = open
            .GroupBy(e => e.Severity)
            .Select(g => new ExceptionSeverityCountDto(g.Key.ToSnake(), g.Count()))
            .OrderBy(s => s.Severity)
            .ToArray();

        var byAge = BuildAgeBuckets(open.Select(e => (now - e.RaisedAt).TotalDays));

        // Root-cause taxonomy breakdown over open findings (P2-A); null/blank → "uncategorised".
        var byRootCause = open
            .GroupBy(e => string.IsNullOrWhiteSpace(e.RootCauseCategory) ? "uncategorised" : e.RootCauseCategory!.Trim())
            .Select(g => new ExceptionRootCauseCountDto(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.RootCauseCategory)
            .ToArray();

        // Average closure time over CLOSED exceptions (days between raised and closed).
        var closed = await db.Exceptions.AsNoTracking()
            .Where(e => e.Status == ExceptionStatus.Closed && e.ClosedAt != null)
            .Select(e => new { e.AuditableEntityId, e.RaisedAt, ClosedAt = e.ClosedAt!.Value })
            .ToListAsync(cancellationToken);

        var avgClosure = closed.Count > 0 ? closed.Average(e => (e.ClosedAt - e.RaisedAt).TotalDays) : (double?)null;

        // By entity: open count + average closure days for that entity's closed exceptions. Resolve names.
        var openByEntity = open.Where(e => e.AuditableEntityId != null)
            .GroupBy(e => e.AuditableEntityId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var closureByEntity = closed.Where(e => e.AuditableEntityId != null)
            .GroupBy(e => e.AuditableEntityId!.Value)
            .ToDictionary(g => g.Key, g => g.Average(e => (e.ClosedAt - e.RaisedAt).TotalDays));

        var entityIds = openByEntity.Keys.ToHashSet();
        var names = await db.AuditUniverseEntities.AsNoTracking()
            .Where(en => entityIds.Contains(en.Id))
            .Select(en => new { en.Id, en.Name })
            .ToDictionaryAsync(en => en.Id, en => en.Name, cancellationToken);

        var byEntity = openByEntity
            .Select(kvp => new ExceptionByEntityDto(
                kvp.Key,
                names.TryGetValue(kvp.Key, out var name) ? name : kvp.Key.ToString(),
                kvp.Value,
                closureByEntity.TryGetValue(kvp.Key, out var days) ? days : null))
            .OrderByDescending(e => e.OpenCount)
            .ThenBy(e => e.EntityName)
            .ToArray();

        return new ExceptionPortfolioDto(open.Count, bySeverity, byAge, byEntity, byRootCause, avgClosure);
    }

    public async Task<SanctionsConsistencyDto> SanctionsConsistencyAsync(CancellationToken cancellationToken = default)
    {
        // Project ONLY the non-identifying fields. subject_user_id is never selected (FR-M7-010 / NFR-SEC-007).
        var cases = await db.SanctionsCases.AsNoTracking()
            .Select(c => new { c.Id, c.Category, c.WithinGridRange })
            .ToListAsync(cancellationToken);

        var appealCountByCase = await db.SanctionsAppeals.AsNoTracking()
            .GroupBy(a => a.SanctionsCaseId)
            .Select(g => new { CaseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CaseId, x => x.Count, cancellationToken);

        var rows = cases
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Category) ? "uncategorised" : c.Category!.Trim())
            .Select(g =>
            {
                var caseCount = g.Count();
                var withinGrid = g.Count(c => c.WithinGridRange);
                var appeals = g.Sum(c => appealCountByCase.TryGetValue(c.Id, out var n) ? n : 0);
                return new SanctionsConsistencyRowDto(
                    g.Key,
                    caseCount,
                    withinGrid,
                    Percent(withinGrid, caseCount),
                    caseCount - withinGrid,
                    appeals,
                    Percent(appeals, caseCount));
            })
            .OrderBy(r => r.BusinessUnit)
            .ToArray();

        var totalCases = cases.Count;
        var totalWithinGrid = cases.Count(c => c.WithinGridRange);
        var totalAppeals = appealCountByCase.Values.Sum();

        return new SanctionsConsistencyDto(
            totalCases, Percent(totalWithinGrid, totalCases), Percent(totalAppeals, totalCases), rows);
    }

    public async Task<IReadOnlyList<PerformanceScorecardDto>> PerformanceScorecardsAsync(CancellationToken cancellationToken = default)
    {
        var audits = await db.Audits.AsNoTracking()
            .Select(a => new { a.Id, a.LeadUserId, a.Status, a.StartDate, a.ActualEndDate })
            .ToListAsync(cancellationToken);

        if (audits.Count == 0)
        {
            return [];
        }

        var auditIds = audits.Select(a => a.Id).ToHashSet();
        var auditLeadById = audits.ToDictionary(a => a.Id, a => a.LeadUserId);

        var exceptions = await db.Exceptions.AsNoTracking()
            .Where(e => auditIds.Contains(e.AuditId))
            .Select(e => new { e.AuditId, e.Status, e.RaisedAt, e.ClosedAt })
            .ToListAsync(cancellationToken);

        var exByLead = exceptions
            .Where(e => auditLeadById.ContainsKey(e.AuditId))
            .Select(e => new { Lead = auditLeadById[e.AuditId], e.Status, e.RaisedAt, e.ClosedAt })
            .GroupBy(e => e.Lead)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return audits
            .GroupBy(a => a.LeadUserId)
            .Select(g =>
            {
                var completed = g.Where(a => a.Status == AuditStatus.Completed && a.ActualEndDate != null).ToArray();
                var avgCycle = completed.Length > 0
                    ? completed.Average(a => a.ActualEndDate!.Value.DayNumber - a.StartDate.DayNumber)
                    : (double?)null;

                var leadExceptions = exByLead.TryGetValue(g.Key, out var ex) ? ex : [];
                var closedEx = leadExceptions.Where(e => e.Status == ExceptionStatus.Closed && e.ClosedAt != null).ToArray();
                var avgExClosure = closedEx.Length > 0
                    ? closedEx.Average(e => (e.ClosedAt!.Value - e.RaisedAt).TotalDays)
                    : (double?)null;

                return new PerformanceScorecardDto(
                    g.Key,
                    g.Count(),
                    completed.Length,
                    avgCycle,
                    leadExceptions.Length,
                    closedEx.Length,
                    avgExClosure);
            })
            .OrderByDescending(s => s.AuditsLed)
            .ThenBy(s => s.AuditLeadUserId)
            .ToArray();
    }

    public async Task<IReadOnlyList<MaterialFindingDto>> MaterialFindingsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Exceptions.AsNoTracking()
            .Where(e => OpenStatuses.Contains(e.Status)
                && (e.Severity == ExceptionSeverity.Critical || e.Severity == ExceptionSeverity.High))
            .OrderByDescending(e => e.Severity)
            .ThenBy(e => e.TargetDate)
            .Select(e => new
            {
                e.Id, e.AuditId, e.Title, e.Severity, e.Status, e.AuditableEntityId, e.RaisedAt, e.TargetDate,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(e => new MaterialFindingDto(
                e.Id, e.AuditId, e.Title, e.Severity.ToSnake(), e.Status.ToSnake(), e.AuditableEntityId, e.RaisedAt, e.TargetDate))
            .ToArray();
    }

    public async Task<PlanStatusDto> PlanStatusAsync(CancellationToken cancellationToken = default)
    {
        var totalPlans = await db.AnnualPlans.AsNoTracking().CountAsync(cancellationToken);

        var byStatus = await db.PlanItems.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = byStatus.ToDictionary(x => x.Status, x => x.Count);
        var planned = counts.GetValueOrDefault(PlanItemStatus.Planned);
        var inProgress = counts.GetValueOrDefault(PlanItemStatus.InProgress);
        var completed = counts.GetValueOrDefault(PlanItemStatus.Completed);
        var deferred = counts.GetValueOrDefault(PlanItemStatus.Deferred);
        var total = planned + inProgress + completed + deferred;

        return new PlanStatusDto(totalPlans, total, planned, inProgress, completed, deferred, Percent(completed, total));
    }

    public async Task<IReadOnlyList<OrgUnitScorecardDto>> OrgUnitScorecardsAsync(CancellationToken cancellationToken = default)
    {
        var orgUnits = await db.OrgUnits.AsNoTracking()
            .Where(o => !o.IsArchived)
            .Select(o => new { o.Id, o.Code, o.Name, o.ParentOrgUnitId })
            .ToListAsync(cancellationToken);
        if (orgUnits.Count == 0)
        {
            return [];
        }

        // Findings/audits inherit their org unit via the auditable entity: entity → OrgUnitId.
        var entityOrg = await db.AuditUniverseEntities.AsNoTracking()
            .Where(e => e.OrgUnitId != null)
            .Select(e => new { e.Id, OrgUnitId = e.OrgUnitId!.Value })
            .ToDictionaryAsync(e => e.Id, e => e.OrgUnitId, cancellationToken);

        // Direct (own-unit) aggregates, before the subtree roll-up.
        var direct = orgUnits.ToDictionary(o => o.Id, _ => new OrgAgg());
        foreach (var orgId in entityOrg.Values)
        {
            if (direct.TryGetValue(orgId, out var a))
            {
                a.Entities++;
            }
        }

        var audits = await db.Audits.AsNoTracking()
            .Where(a => a.AuditableEntityId != null)
            .Select(a => new { EntityId = a.AuditableEntityId!.Value, a.Status })
            .ToListAsync(cancellationToken);
        foreach (var au in audits)
        {
            if (entityOrg.TryGetValue(au.EntityId, out var orgId) && direct.TryGetValue(orgId, out var a))
            {
                if (au.Status == AuditStatus.Completed)
                {
                    a.AuditsCompleted++;
                }
                else if (au.Status is AuditStatus.Planned or AuditStatus.InProgress or AuditStatus.UnderReview)
                {
                    a.AuditsInFlight++;
                }
            }
        }

        var findings = await db.Exceptions.AsNoTracking()
            .Where(e => e.AuditableEntityId != null)
            .Select(e => new { EntityId = e.AuditableEntityId!.Value, e.Status, e.Severity, e.RaisedAt, e.ClosedAt })
            .ToListAsync(cancellationToken);
        foreach (var f in findings)
        {
            if (!entityOrg.TryGetValue(f.EntityId, out var orgId) || !direct.TryGetValue(orgId, out var a))
            {
                continue;
            }

            if (OpenStatuses.Contains(f.Status))
            {
                a.OpenFindings++;
                if (f.Severity == ExceptionSeverity.Critical)
                {
                    a.CriticalOpen++;
                }
                else if (f.Severity == ExceptionSeverity.High)
                {
                    a.HighOpen++;
                }
            }
            else if (f.Status == ExceptionStatus.Closed && f.ClosedAt is { } closedAt)
            {
                a.ClosedFindings++;
                a.ClosureDaysSum += (closedAt - f.RaisedAt).TotalDays;
            }
        }

        // Roll each unit up its subtree (unit + all descendants) and emit in pre-order for tree rendering.
        var childrenByParent = orgUnits.Where(o => o.ParentOrgUnitId != null)
            .GroupBy(o => o.ParentOrgUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Name).Select(o => o.Id).ToArray());
        var byId = orgUnits.ToDictionary(o => o.Id);
        var ids = byId.Keys.ToHashSet();

        var result = new List<OrgUnitScorecardDto>();
        var roots = orgUnits
            .Where(o => o.ParentOrgUnitId is not { } p || !ids.Contains(p))
            .OrderBy(o => o.Name)
            .ToArray();
        var stack = new Stack<(Guid Id, int Depth)>();
        foreach (var root in roots.Reverse())
        {
            stack.Push((root.Id, 0));
        }

        var emitted = new HashSet<Guid>();
        while (stack.Count > 0)
        {
            var (id, depth) = stack.Pop();
            if (!emitted.Add(id) || !byId.TryGetValue(id, out var o))
            {
                continue;
            }

            var agg = RollUp(id, childrenByParent, direct);
            result.Add(new OrgUnitScorecardDto(
                o.Id, o.Code, o.Name, o.ParentOrgUnitId, depth,
                agg.Entities, agg.AuditsCompleted, agg.AuditsInFlight, agg.OpenFindings,
                agg.CriticalOpen, agg.HighOpen, agg.ClosedFindings,
                agg.ClosedFindings > 0 ? agg.ClosureDaysSum / agg.ClosedFindings : null));

            if (childrenByParent.TryGetValue(id, out var kids))
            {
                foreach (var kid in kids.Reverse())
                {
                    stack.Push((kid, depth + 1));
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<BudgetVsActualDto>> BudgetVsActualAsync(CancellationToken cancellationToken = default)
    {
        // Actual = sum of live time entries per audit (the soft-delete filter excludes deleted rows).
        var actualByAudit = await db.TimeEntries.AsNoTracking()
            .GroupBy(t => t.AuditId)
            .Select(g => new { AuditId = g.Key, Hours = g.Sum(t => t.Hours) })
            .ToListAsync(cancellationToken);
        var actualMap = actualByAudit.ToDictionary(a => a.AuditId, a => a.Hours);
        var loggedIds = actualByAudit.Select(a => a.AuditId).ToList();

        // Include audits with a budget OR any logged time.
        var audits = await db.Audits.AsNoTracking()
            .Where(a => a.BudgetedHours != null || loggedIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Name, a.Status, a.LeadUserId, a.BudgetedHours })
            .ToListAsync(cancellationToken);

        return audits
            .Select(a =>
            {
                var actual = actualMap.TryGetValue(a.Id, out var h) ? h : 0m;
                var budget = a.BudgetedHours;
                return new BudgetVsActualDto(
                    a.Id, a.Name, a.Status.ToSnake(), a.LeadUserId, budget, actual,
                    budget is { } b ? b - actual : null,
                    budget is { } bd && bd > 0 ? (double)(actual / bd) * 100d : null);
            })
            .OrderByDescending(r => r.ActualHours)
            .ToArray();
    }

    public async Task<IReadOnlyList<AuditorUtilisationDto>> UtilisationByUserAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.TimeEntries.AsNoTracking()
            .Select(t => new { t.UserId, t.AuditId, t.Category, t.Hours })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(t => t.UserId)
            .Select(g => new AuditorUtilisationDto(
                g.Key,
                g.Sum(t => t.Hours),
                g.Select(t => t.AuditId).Distinct().Count(),
                g.GroupBy(t => t.Category)
                    .Select(c => new UtilisationCategoryDto(c.Key.ToSnake(), c.Sum(t => t.Hours)))
                    .OrderByDescending(c => c.Hours)
                    .ToArray()))
            .OrderByDescending(u => u.TotalHours)
            .ToArray();
    }

    public async Task<RiskHeatmapDto> RiskHeatmapAsync(CancellationToken cancellationToken = default)
    {
        // Open risks only; the CURRENT position is residual when assessed, else inherent. Soft-deleted rows are
        // excluded by the global query filter.
        var risks = await db.Risks.AsNoTracking()
            .Where(r => r.Status != RiskStatus.Closed)
            .Select(r => new
            {
                L = r.ResidualLikelihood ?? r.InherentLikelihood,
                I = r.ResidualImpact ?? r.InherentImpact,
            })
            .ToListAsync(cancellationToken);

        var cells = risks
            .GroupBy(r => new { r.L, r.I })
            .Select(g =>
            {
                var score = g.Key.L * g.Key.I;
                return new RiskHeatmapCellDto(g.Key.L, g.Key.I, score, RiskBands.Band(score).ToSnake(), g.Count());
            })
            .OrderBy(c => c.Impact).ThenBy(c => c.Likelihood)
            .ToArray();

        return new RiskHeatmapDto(risks.Count, cells);
    }

    public async Task<RiskRegisterSummaryDto> RiskSummaryAsync(CancellationToken cancellationToken = default)
    {
        var all = await db.Risks.AsNoTracking()
            .Select(r => new
            {
                r.Status,
                r.Category,
                r.TreatmentStrategy,
                L = r.ResidualLikelihood ?? r.InherentLikelihood,
                I = r.ResidualImpact ?? r.InherentImpact,
                r.NextReviewDate,
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var open = all.Where(r => r.Status != RiskStatus.Closed).ToArray();

        var byBand = open
            .GroupBy(r => RiskBands.Band(r.L * r.I))
            .Select(g => new RiskCountDto(g.Key.ToSnake(), g.Count()))
            .OrderByDescending(c => c.Count).ToArray();
        var byStatus = all
            .GroupBy(r => r.Status)
            .Select(g => new RiskCountDto(g.Key.ToSnake(), g.Count()))
            .OrderByDescending(c => c.Count).ToArray();
        var byCategory = open
            .GroupBy(r => r.Category)
            .Select(g => new RiskCountDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Count).ToArray();
        var byStrategy = open
            .Where(r => r.TreatmentStrategy != null)
            .GroupBy(r => r.TreatmentStrategy!.Value)
            .Select(g => new RiskCountDto(g.Key.ToSnake(), g.Count()))
            .OrderByDescending(c => c.Count).ToArray();
        var overdueReview = open.Count(r => r.NextReviewDate is { } d && d < today);

        return new RiskRegisterSummaryDto(all.Count, open.Length, all.Count - open.Length, overdueReview, byBand, byStatus, byCategory, byStrategy);
    }

    public async Task<ControlEffectivenessSummaryDto> ControlEffectivenessAsync(CancellationToken cancellationToken = default)
    {
        // Active controls only (soft-deleted excluded by the global filter).
        var controls = await db.Controls.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Effectiveness, c.ControlType })
            .ToListAsync(cancellationToken);

        var byEffectiveness = controls
            .GroupBy(c => c.Effectiveness)
            .Select(g => new ControlCountDto(g.Key.ToSnake(), g.Count()))
            .OrderByDescending(c => c.Count).ToArray();
        var byType = controls
            .GroupBy(c => c.ControlType)
            .Select(g => new ControlCountDto(g.Key.ToSnake(), g.Count()))
            .OrderByDescending(c => c.Count).ToArray();

        var tested = controls.Count(c => c.Effectiveness != ControlEffectiveness.NotTested);
        var ineffective = controls.Count(c => c.Effectiveness == ControlEffectiveness.Ineffective);
        return new ControlEffectivenessSummaryDto(controls.Count, tested, ineffective, byEffectiveness, byType);
    }

    public async Task<IReadOnlyList<ComplianceByRegulationRowDto>> ComplianceByRegulationAsync(CancellationToken cancellationToken = default)
    {
        var regulations = await db.Regulations.AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => new { r.Id, r.Code, r.Name, r.Authority })
            .ToListAsync(cancellationToken);

        // Linked + open finding counts per regulation, via the link table joined to the exception's status.
        var linkAgg = await db.ExceptionRegulationLinks.AsNoTracking()
            .Join(db.Exceptions, l => l.ExceptionId, e => e.Id, (l, e) => new { l.RegulationId, e.Status })
            .GroupBy(x => x.RegulationId)
            .Select(g => new
            {
                RegulationId = g.Key,
                Linked = g.Count(),
                Open = g.Count(x => OpenStatuses.Contains(x.Status)),
            })
            .ToListAsync(cancellationToken);
        var byReg = linkAgg.ToDictionary(a => a.RegulationId);

        return regulations
            .Select(r =>
            {
                byReg.TryGetValue(r.Id, out var a);
                return new ComplianceByRegulationRowDto(r.Id, r.Code, r.Name, r.Authority, a?.Linked ?? 0, a?.Open ?? 0);
            })
            .OrderByDescending(x => x.OpenFindings).ThenByDescending(x => x.LinkedFindings).ThenBy(x => x.Code)
            .ToArray();
    }

    public async Task<FindingFollowUpSummaryDto> FindingFollowUpAsync(CancellationToken cancellationToken = default)
    {
        var findings = await db.Exceptions.AsNoTracking()
            .Select(e => new { e.Status, e.ReopenCount, HasResponse = e.ManagementResponseDecision != null })
            .ToListAsync(cancellationToken);

        var total = findings.Count;
        var closed = findings.Count(f => f.Status == ExceptionStatus.Closed);
        var reopened = findings.Count(f => f.ReopenCount > 0);
        var withResponse = findings.Count(f => f.HasResponse);

        var verifiedFindings = await db.FindingVerifications.AsNoTracking()
            .Select(v => v.ExceptionId).Distinct().CountAsync(cancellationToken);

        var byResult = await db.FindingVerifications.AsNoTracking()
            .GroupBy(v => v.Result)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byVerificationResult = byResult
            .Select(g => new VerificationResultCountDto(g.Key.ToSnake(), g.Count))
            .OrderByDescending(r => r.Count).ThenBy(r => r.Result)
            .ToArray();

        return new FindingFollowUpSummaryDto(total, closed, reopened, withResponse, verifiedFindings, byVerificationResult);
    }

    /// <summary>Sums a unit's own aggregate with every descendant's (DFS; cycle-guarded).</summary>
    private static OrgAgg RollUp(Guid rootId, IReadOnlyDictionary<Guid, Guid[]> childrenByParent, IReadOnlyDictionary<Guid, OrgAgg> direct)
    {
        var total = new OrgAgg();
        var stack = new Stack<Guid>();
        stack.Push(rootId);
        var seen = new HashSet<Guid>();
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!seen.Add(id))
            {
                continue;
            }

            if (direct.TryGetValue(id, out var d))
            {
                total.Add(d);
            }

            if (childrenByParent.TryGetValue(id, out var kids))
            {
                foreach (var kid in kids)
                {
                    stack.Push(kid);
                }
            }
        }

        return total;
    }

    private sealed class OrgAgg
    {
        public int Entities;
        public int AuditsCompleted;
        public int AuditsInFlight;
        public int OpenFindings;
        public int CriticalOpen;
        public int HighOpen;
        public int ClosedFindings;
        public double ClosureDaysSum;

        public void Add(OrgAgg other)
        {
            Entities += other.Entities;
            AuditsCompleted += other.AuditsCompleted;
            AuditsInFlight += other.AuditsInFlight;
            OpenFindings += other.OpenFindings;
            CriticalOpen += other.CriticalOpen;
            HighOpen += other.HighOpen;
            ClosedFindings += other.ClosedFindings;
            ClosureDaysSum += other.ClosureDaysSum;
        }
    }

    private static IReadOnlyList<ExceptionAgeBucketDto> BuildAgeBuckets(IEnumerable<double> agesInDays)
    {
        int b0 = 0, b1 = 0, b2 = 0, b3 = 0;
        foreach (var age in agesInDays)
        {
            if (age <= 30)
            {
                b0++;
            }
            else if (age <= 60)
            {
                b1++;
            }
            else if (age <= 90)
            {
                b2++;
            }
            else
            {
                b3++;
            }
        }

        return
        [
            new ExceptionAgeBucketDto("0-30", b0),
            new ExceptionAgeBucketDto("31-60", b1),
            new ExceptionAgeBucketDto("61-90", b2),
            new ExceptionAgeBucketDto("90+", b3),
        ];
    }

    private static decimal Percent(int numerator, int denominator)
        => denominator == 0 ? 0m : Math.Round(numerator * 100m / denominator, 2);
}
