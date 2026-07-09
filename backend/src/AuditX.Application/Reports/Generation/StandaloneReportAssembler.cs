using System.Globalization;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Universe;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.ReferenceData;

namespace AuditX.Application.Reports.Generation;

/// <summary>
/// Composes a <see cref="StandaloneReportModel"/> for a cross-audit report kind from the LIVE M9 analytics + coverage
/// read ports at generation time (M8). Pure composition service (no port of its own); mirrors
/// <see cref="ReportContentAssembler"/> but for the function-wide / analytics shapes rather than a single audit.
///
/// NAME RESOLUTION: every machine identifier is resolved to a human label before it reaches the renderer — entity/
/// audit/category CODES via the reference-data lists, auditable-entity GUIDs via the universe, and audit-lead GUIDs
/// via the directory. A report never shows a raw code or GUID where a name exists.
///
/// SCOPE GUARD: only aggregated analytics are read — the sanctions rollup is by category and carries no subject
/// identity (FR-M7-010), matching the M9 query service's own invariant.
/// </summary>
public sealed class StandaloneReportAssembler(
    IAnalyticsQueryService analytics,
    ICoverageQueryService coverage,
    IReferenceDataRepository references,
    IUserRepository users,
    IAuditUniverseRepository universe,
    IRecurrenceClusterRepository clusters,
    IClock clock)
{
    private const int CoverageWindowMonths = 12;
    private const int NotAuditedMonths = 12;

    public async Task<StandaloneReportModel> AssembleAsync(ReportKind kind, int versionNumber, CancellationToken cancellationToken)
    {
        var sections = kind switch
        {
            ReportKind.ExecutiveSummary => await BuildExecutiveSummaryAsync(cancellationToken),
            ReportKind.AnnualPlanStatus => await BuildPlanStatusAsync(cancellationToken),
            ReportKind.KpiPack => await BuildKpiPackAsync(cancellationToken),
            ReportKind.AuditCoverage => await BuildCoverageAsync(cancellationToken),
            ReportKind.FindingsRegister => await BuildFindingsRegisterAsync(cancellationToken),
            ReportKind.SanctionsConsistency => await BuildSanctionsAsync(cancellationToken),
            ReportKind.PerformanceScorecards => await BuildScorecardsAsync(cancellationToken),
            _ => throw new DomainException("report.kind_not_standalone", "The report kind is not a standalone kind."),
        };

        var now = clock.UtcNow;
        return new StandaloneReportModel(
            kind,
            StandaloneReportModel.TitleFor(kind),
            $"Internal Audit — generated {now:yyyy-MM-dd} (UTC)",
            versionNumber,
            now,
            sections);
    }

    /* ---- Kind builders ---- */

    private async Task<IReadOnlyList<StandaloneSection>> BuildExecutiveSummaryAsync(CancellationToken ct)
    {
        var perf = await analytics.FunctionPerformanceAsync(ct);
        var portfolio = await analytics.ExceptionPortfolioAsync(ct);
        var plan = await analytics.PlanStatusAsync(ct);
        var material = await analytics.MaterialFindingsAsync(ct);

        return
        [
            FunctionPerformanceSection(perf),
            ExceptionPortfolioSection(portfolio, includeAgeing: false),
            PlanStatusSection(plan),
            MaterialFindingsSection(material),
        ];
    }

    private async Task<IReadOnlyList<StandaloneSection>> BuildPlanStatusAsync(CancellationToken ct)
    {
        var plan = await analytics.PlanStatusAsync(ct);
        var perf = await analytics.FunctionPerformanceAsync(ct);

        return
        [
            PlanStatusSection(plan),
            new StandaloneSection("Plan execution",
            [
                new StandaloneMetric("Plan items (total)", perf.PlanItemsTotal.ToString(Culture)),
                new StandaloneMetric("Plan items completed", perf.PlanItemsCompleted.ToString(Culture)),
                new StandaloneMetric("Plan execution", Percent(perf.PlanExecutionPercent)),
                new StandaloneMetric("Audits in flight", perf.AuditsInFlight.ToString(Culture)),
                new StandaloneMetric("Audits completed", perf.AuditsCompleted.ToString(Culture)),
            ]),
        ];
    }

    private async Task<IReadOnlyList<StandaloneSection>> BuildKpiPackAsync(CancellationToken ct)
    {
        var perf = await analytics.FunctionPerformanceAsync(ct);
        var portfolio = await analytics.ExceptionPortfolioAsync(ct);
        var plan = await analytics.PlanStatusAsync(ct);
        var sanctions = await analytics.SanctionsConsistencyAsync(ct);
        var matrix = await coverage.CoverageMatrixAsync(CoverageWindowMonths, ct);
        var entityLabels = await LabelMapAsync(ReferenceDataCategories.EntityType, ct);
        var auditLabels = await LabelMapAsync(ReferenceDataCategories.AuditType, ct);
        var sanctionLabels = await LabelMapAsync(ReferenceDataCategories.SanctionCategory, ct);

        return
        [
            FunctionPerformanceSection(perf),
            ExceptionPortfolioSection(portfolio, includeAgeing: true),
            PlanStatusSection(plan),
            SanctionsConsistencySection(sanctions, sanctionLabels),
            CoverageSection(matrix, entityLabels, auditLabels),
        ];
    }

    private async Task<IReadOnlyList<StandaloneSection>> BuildCoverageAsync(CancellationToken ct)
    {
        var matrix = await coverage.CoverageMatrixAsync(CoverageWindowMonths, ct);
        var notAudited = await coverage.NotAuditedSinceAsync(NotAuditedMonths, null, ct);
        var gaps = await coverage.HighRiskGapsAsync(NotAuditedMonths, null, ct);
        var entityLabels = await LabelMapAsync(ReferenceDataCategories.EntityType, ct);
        var auditLabels = await LabelMapAsync(ReferenceDataCategories.AuditType, ct);

        return
        [
            CoverageSection(matrix, entityLabels, auditLabels),
            NotAuditedSection(notAudited, entityLabels),
            HighRiskGapsSection(gaps, entityLabels),
        ];
    }

    private async Task<IReadOnlyList<StandaloneSection>> BuildFindingsRegisterAsync(CancellationToken ct)
    {
        var portfolio = await analytics.ExceptionPortfolioAsync(ct);
        var material = await analytics.MaterialFindingsAsync(ct);
        var tracked = await clusters.ListTrackedAsync(ct);
        var categoryLabels = await LabelMapAsync(ReferenceDataCategories.ExceptionCategory, ct);
        var entityNames = await ResolveEntityNamesAsync(tracked.Select(c => c.AuditableEntityId), ct);

        return
        [
            ExceptionPortfolioSection(portfolio, includeAgeing: true),
            ExceptionByEntitySection(portfolio),
            MaterialFindingsSection(material),
            RecurrenceSection(tracked, entityNames, categoryLabels),
        ];
    }

    private async Task<IReadOnlyList<StandaloneSection>> BuildSanctionsAsync(CancellationToken ct)
    {
        var sanctions = await analytics.SanctionsConsistencyAsync(ct);
        var sanctionLabels = await LabelMapAsync(ReferenceDataCategories.SanctionCategory, ct);
        return [SanctionsConsistencySection(sanctions, sanctionLabels)];
    }

    private async Task<IReadOnlyList<StandaloneSection>> BuildScorecardsAsync(CancellationToken ct)
    {
        var scorecards = await analytics.PerformanceScorecardsAsync(ct);
        var names = await ResolveUserNamesAsync(scorecards.Select(s => s.AuditLeadUserId), ct);

        if (scorecards.Count == 0)
        {
            return [new StandaloneSection("Auditor performance", [], Note: "No completed audits to score in the period.")];
        }

        var table = new StandaloneTable(
            ["Auditor", "Audits led", "Completed", "Avg cycle (days)", "Exceptions raised", "Exceptions closed", "Avg closure (days)"],
            scorecards.Select(s => (IReadOnlyList<string>)
            [
                names.TryGetValue(s.AuditLeadUserId, out var n) ? n : "Unknown auditor",
                s.AuditsLed.ToString(Culture),
                s.AuditsCompleted.ToString(Culture),
                Days(s.AverageCycleDays),
                s.ExceptionsRaised.ToString(Culture),
                s.ExceptionsClosed.ToString(Culture),
                Days(s.AverageExceptionClosureDays),
            ]).ToArray());
        return [new StandaloneSection("Auditor performance", [new StandaloneMetric("Auditors", scorecards.Count.ToString(Culture))], table)];
    }

    /* ---- Section builders ---- */

    private static StandaloneSection FunctionPerformanceSection(FunctionPerformanceDto p) =>
        new("Function performance",
        [
            new StandaloneMetric("Audits in flight", p.AuditsInFlight.ToString(Culture)),
            new StandaloneMetric("Audits completed", p.AuditsCompleted.ToString(Culture)),
            new StandaloneMetric("Plan items (total)", p.PlanItemsTotal.ToString(Culture)),
            new StandaloneMetric("Plan items completed", p.PlanItemsCompleted.ToString(Culture)),
            new StandaloneMetric("Plan execution", Percent(p.PlanExecutionPercent)),
            new StandaloneMetric("Open exception backlog", p.OpenExceptionBacklog.ToString(Culture)),
            new StandaloneMetric("Closed exceptions", p.ClosedExceptions.ToString(Culture)),
            new StandaloneMetric("Closure rate", Percent(p.ClosureRatePercent)),
        ]);

    private static StandaloneSection ExceptionPortfolioSection(ExceptionPortfolioDto p, bool includeAgeing)
    {
        var metrics = new List<StandaloneMetric>
        {
            new("Total open exceptions", p.TotalOpen.ToString(Culture)),
            new("Average closure (days)", Days(p.AverageClosureDays)),
        };
        if (includeAgeing)
        {
            metrics.AddRange(p.ByAgeBucket.Select(b => new StandaloneMetric($"Ageing {b.Bucket} days", b.Count.ToString(Culture))));
        }

        var severityTable = new StandaloneTable(
            ["Severity", "Open"],
            p.BySeverity.Select(s => (IReadOnlyList<string>)[Titleise(s.Severity), s.Count.ToString(Culture)]).ToArray());
        return new StandaloneSection("Exception portfolio", metrics, severityTable);
    }

    private static StandaloneSection ExceptionByEntitySection(ExceptionPortfolioDto p)
    {
        if (p.ByEntity.Count == 0)
        {
            return new StandaloneSection("Open exceptions by entity", [], Note: "No open exceptions.");
        }

        // EntityName is already resolved by the analytics service (falls back to the id only if the entity is gone).
        var table = new StandaloneTable(
            ["Entity", "Open", "Avg closure (days)"],
            p.ByEntity.Select(e => (IReadOnlyList<string>)
            [
                e.EntityName,
                e.OpenCount.ToString(Culture),
                Days(e.AverageClosureDays),
            ]).ToArray());
        return new StandaloneSection("Open exceptions by entity", [], table);
    }

    private static StandaloneSection PlanStatusSection(PlanStatusDto p) =>
        new("Annual plan status",
        [
            new StandaloneMetric("Plans", p.TotalPlans.ToString(Culture)),
            new StandaloneMetric("Plan items (total)", p.TotalItems.ToString(Culture)),
            new StandaloneMetric("Planned", p.Planned.ToString(Culture)),
            new StandaloneMetric("In progress", p.InProgress.ToString(Culture)),
            new StandaloneMetric("Completed", p.Completed.ToString(Culture)),
            new StandaloneMetric("Deferred", p.Deferred.ToString(Culture)),
            new StandaloneMetric("Completion", Percent(p.CompletionPercent)),
        ]);

    private static StandaloneSection MaterialFindingsSection(IReadOnlyList<MaterialFindingDto> findings)
    {
        if (findings.Count == 0)
        {
            return new StandaloneSection("Material findings", [], Note: "No open critical or high findings.");
        }

        var table = new StandaloneTable(
            ["Title", "Severity", "Status", "Raised", "Target"],
            findings.Select(f => (IReadOnlyList<string>)
            [
                f.Title,
                Titleise(f.Severity),
                Titleise(f.Status),
                f.RaisedAt.ToString("yyyy-MM-dd", Culture),
                f.TargetDate.ToString("yyyy-MM-dd", Culture),
            ]).ToArray());
        return new StandaloneSection("Material findings", [new StandaloneMetric("Open critical / high", findings.Count.ToString(Culture))], table);
    }

    private StandaloneSection RecurrenceSection(
        IReadOnlyList<Domain.Analytics.RecurrenceCluster> tracked,
        IReadOnlyDictionary<Guid, string> entityNames,
        IReadOnlyDictionary<string, string> categoryLabels)
    {
        if (tracked.Count == 0)
        {
            return new StandaloneSection("Recurrence clusters", [], Note: "No recurring findings detected.");
        }

        var table = new StandaloneTable(
            ["Entity", "Category", "Closed findings", "Window (months)", "First", "Last"],
            tracked.Select(c => (IReadOnlyList<string>)
            [
                entityNames.TryGetValue(c.AuditableEntityId, out var n) ? n : "Unknown entity",
                Label(categoryLabels, c.Category),
                c.ClosedExceptionCount.ToString(Culture),
                c.WindowMonths.ToString(Culture),
                c.FirstOccurredAt.ToString("yyyy-MM-dd", Culture),
                c.LastOccurredAt.ToString("yyyy-MM-dd", Culture),
            ]).ToArray());
        return new StandaloneSection("Recurrence clusters", [new StandaloneMetric("Clusters", tracked.Count.ToString(Culture))], table);
    }

    private static StandaloneSection SanctionsConsistencySection(SanctionsConsistencyDto s, IReadOnlyDictionary<string, string> categoryLabels)
    {
        var metrics = new StandaloneMetric[]
        {
            new("Total cases", s.TotalCases.ToString(Culture)),
            new("Grid adherence", Percent(s.OverallGridAdherencePercent)),
            new("Appeal rate", Percent(s.OverallAppealRatePercent)),
        };

        if (s.ByBusinessUnit.Count == 0)
        {
            return new StandaloneSection("Sanctions consistency", metrics, Note: "No sanction cases in scope.");
        }

        var table = new StandaloneTable(
            ["Category", "Cases", "Grid adherence", "Appeal rate"],
            s.ByBusinessUnit.Select(r => (IReadOnlyList<string>)
            [
                Label(categoryLabels, r.BusinessUnit),
                r.CaseCount.ToString(Culture),
                Percent(r.GridAdherencePercent),
                Percent(r.AppealRatePercent),
            ]).ToArray());
        return new StandaloneSection("Sanctions consistency", metrics, table);
    }

    private static StandaloneSection CoverageSection(
        CoverageMatrix matrix, IReadOnlyDictionary<string, string> entityLabels, IReadOnlyDictionary<string, string> auditLabels)
    {
        var total = matrix.Cells.Sum(row => row.Sum());
        var metrics = new StandaloneMetric[]
        {
            new("Completed audits (last 12 months)", total.ToString(Culture)),
            new("Entity types", matrix.Rows.Count.ToString(Culture)),
            new("Audit types", matrix.Columns.Count.ToString(Culture)),
        };

        if (matrix.Rows.Count == 0 || matrix.Columns.Count == 0)
        {
            return new StandaloneSection("Coverage matrix", metrics, Note: "No completed audits in the coverage window.");
        }

        var columns = new List<string> { "Entity type" };
        columns.AddRange(matrix.Columns.Select(c => Label(auditLabels, c)));
        var rows = matrix.Rows.Select((entityType, i) =>
        {
            var cells = new List<string> { Label(entityLabels, entityType) };
            cells.AddRange((matrix.Cells[i] ?? []).Select(v => v.ToString(Culture)));
            return (IReadOnlyList<string>)cells;
        }).ToArray();
        return new StandaloneSection("Coverage matrix", metrics, new StandaloneTable(columns, rows));
    }

    private StandaloneSection NotAuditedSection(IReadOnlyList<NotAuditedRow> rows, IReadOnlyDictionary<string, string> entityLabels)
    {
        if (rows.Count == 0)
        {
            return new StandaloneSection($"Not audited in {NotAuditedMonths} months", [], Note: "Every entity has been audited within the window.");
        }

        var table = new StandaloneTable(
            ["Entity", "Type", "Last audited"],
            rows.Select(r => (IReadOnlyList<string>)
            [
                r.Name,
                Label(entityLabels, r.EntityType),
                r.LastAuditedAt is { } at ? at.ToString("yyyy-MM-dd", Culture) : "Never",
            ]).ToArray());
        return new StandaloneSection($"Not audited in {NotAuditedMonths} months", [new StandaloneMetric("Entities", rows.Count.ToString(Culture))], table);
    }

    private StandaloneSection HighRiskGapsSection(IReadOnlyList<HighRiskGapRow> rows, IReadOnlyDictionary<string, string> entityLabels)
    {
        if (rows.Count == 0)
        {
            return new StandaloneSection("High-risk coverage gaps", [], Note: "No high-risk entities lack a recent audit.");
        }

        var table = new StandaloneTable(
            ["Entity", "Type", "Residual risk", "Last audited"],
            rows.Select(r => (IReadOnlyList<string>)
            [
                r.Name,
                Label(entityLabels, r.EntityType),
                r.CompositeResidualScore is { } s ? s.ToString("0.##", Culture) : "—",
                r.LastAuditedAt is { } at ? at.ToString("yyyy-MM-dd", Culture) : "Never",
            ]).ToArray());
        return new StandaloneSection("High-risk coverage gaps", [new StandaloneMetric("Entities", rows.Count.ToString(Culture))], table);
    }

    /* ---- Name resolution ---- */

    private async Task<Dictionary<string, string>> LabelMapAsync(string category, CancellationToken ct)
    {
        var items = await references.ListByCategoryAsync(category, includeInactive: true, ct);
        return items.ToDictionary(i => i.Code, i => i.Label, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves a set of auditable-entity ids to their names (falls back to a per-id lookup for stragglers).</summary>
    private async Task<Dictionary<Guid, string>> ResolveEntityNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToHashSet();
        var map = new Dictionary<Guid, string>();
        if (wanted.Count == 0)
        {
            return map;
        }

        foreach (var entity in await universe.GetForCoverageAsync(null, ct))
        {
            if (wanted.Contains(entity.Id))
            {
                map[entity.Id] = entity.Name;
            }
        }

        foreach (var id in wanted.Where(id => !map.ContainsKey(id)))
        {
            var entity = await universe.GetByIdAsync(id, ct);
            if (entity is not null)
            {
                map[id] = entity.Name;
            }
        }

        return map;
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToArray();
        if (wanted.Length == 0)
        {
            return [];
        }

        var found = await users.GetByIdsAsync(wanted, ct);
        return found.ToDictionary(u => u.Id, u => u.DisplayName);
    }

    /* ---- Formatting helpers (invariant → deterministic canonical bytes) ---- */

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Percent(decimal value) => value.ToString("0.#", Culture) + "%";

    private static string Days(double? value) => value is { } d ? d.ToString("0.#", Culture) : "—";

    private static string Label(IReadOnlyDictionary<string, string> map, string? code)
        => string.IsNullOrWhiteSpace(code) ? "—" : (map.TryGetValue(code, out var label) ? label : Titleise(code));

    private static string Titleise(string snake)
        => string.IsNullOrWhiteSpace(snake)
            ? snake
            : string.Join(' ', snake.Split('_', ' ').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
