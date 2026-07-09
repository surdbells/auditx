using System.Globalization;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Universe;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Reports.Generation;

/// <summary>
/// Composes a <see cref="StandaloneReportModel"/> for a cross-audit report kind from the LIVE M9 analytics + coverage
/// read ports at generation time (M8). Pure composition service (no port of its own); mirrors
/// <see cref="ReportContentAssembler"/> but for the function-wide / analytics shapes rather than a single audit.
///
/// SCOPE GUARD: only aggregated analytics are read — the sanctions rollup is by business unit and carries no subject
/// identity (FR-M7-010), matching the M9 query service's own invariant.
/// </summary>
public sealed class StandaloneReportAssembler(
    IAnalyticsQueryService analytics, ICoverageQueryService coverage, IClock clock)
{
    private const int CoverageWindowMonths = 12;

    public async Task<StandaloneReportModel> AssembleAsync(ReportKind kind, int versionNumber, CancellationToken cancellationToken)
    {
        var sections = kind switch
        {
            ReportKind.ExecutiveSummary => await BuildExecutiveSummaryAsync(cancellationToken),
            ReportKind.AnnualPlanStatus => await BuildPlanStatusAsync(cancellationToken),
            ReportKind.KpiPack => await BuildKpiPackAsync(cancellationToken),
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

        return
        [
            FunctionPerformanceSection(perf),
            ExceptionPortfolioSection(portfolio, includeAgeing: true),
            PlanStatusSection(plan),
            SanctionsConsistencySection(sanctions),
            CoverageSection(matrix),
        ];
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

        var severityTable = new StandaloneTable(
            ["Severity", "Open"],
            p.BySeverity.Select(s => (IReadOnlyList<string>)[Titleise(s.Severity), s.Count.ToString(Culture)]).ToArray());

        var section = new StandaloneSection("Exception portfolio", metrics, severityTable);
        if (!includeAgeing || p.ByAgeBucket.Count == 0)
        {
            return section;
        }

        // The KPI pack additionally carries the ageing distribution as extra metrics.
        var withAgeing = metrics.Concat(p.ByAgeBucket.Select(b => new StandaloneMetric($"Ageing {b.Bucket} days", b.Count.ToString(Culture)))).ToArray();
        return new StandaloneSection("Exception portfolio", withAgeing, severityTable);
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

    private static StandaloneSection SanctionsConsistencySection(SanctionsConsistencyDto s)
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
            ["Business unit", "Cases", "Grid adherence", "Appeal rate"],
            s.ByBusinessUnit.Select(r => (IReadOnlyList<string>)
            [
                r.BusinessUnit,
                r.CaseCount.ToString(Culture),
                Percent(r.GridAdherencePercent),
                Percent(r.AppealRatePercent),
            ]).ToArray());
        return new StandaloneSection("Sanctions consistency", metrics, table);
    }

    private static StandaloneSection CoverageSection(CoverageMatrix matrix)
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
        columns.AddRange(matrix.Columns);
        var rows = matrix.Rows.Select((entityType, i) =>
        {
            var cells = new List<string> { entityType };
            cells.AddRange((matrix.Cells[i] ?? []).Select(v => v.ToString(Culture)));
            return (IReadOnlyList<string>)cells;
        }).ToArray();
        return new StandaloneSection("Coverage matrix", metrics, new StandaloneTable(columns, rows));
    }

    /* ---- Formatting helpers (invariant → deterministic canonical bytes) ---- */

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Percent(decimal value) => value.ToString("0.#", Culture) + "%";

    private static string Days(double? value) => value is { } d ? d.ToString("0.#", Culture) : "—";

    private static string Titleise(string snake)
        => string.IsNullOrWhiteSpace(snake)
            ? snake
            : string.Join(' ', snake.Split('_', ' ').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
