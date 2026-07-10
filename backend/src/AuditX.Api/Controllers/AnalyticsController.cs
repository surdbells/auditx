using AuditX.Api.Authorization;
using AuditX.Application.Analytics.Commands;
using AuditX.Application.Analytics.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// M9 analytics KPI endpoints. Each KPI is ViewAnalytics-gated and read-only. Performance scorecards require the
/// stricter PerformanceAnalyticsView (and self-suppress the caller's own row unless they hold CIA — in-handler).
/// EVERY projection physically omits the sanctions subject identity (FR-M7-010 / NFR-SEC-007). Ad-hoc query,
/// predictive routes and the AC pack artefact are deferred (see m9_blueprint.md).
/// </summary>
[Authorize]
[Route("api/v1/analytics")]
public sealed class AnalyticsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("function-performance")]
    public async Task<IActionResult> FunctionPerformance(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new FunctionPerformanceQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("exception-portfolio")]
    public async Task<IActionResult> ExceptionPortfolio(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ExceptionPortfolioQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("sanctions-consistency")]
    public async Task<IActionResult> SanctionsConsistency(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new SanctionsConsistencyQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("material-findings")]
    public async Task<IActionResult> MaterialFindings(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new MaterialFindingsQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("plan-status")]
    public async Task<IActionResult> PlanStatus(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new PlanStatusQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("coverage")]
    public async Task<IActionResult> Coverage([FromQuery] int? windowMonths, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new AnalyticsCoverageQuery(windowMonths ?? 12), cancellationToken));

    [RequirePermission(PermissionKeys.PerformanceAnalyticsView)]
    [HttpGet("performance-scorecards")]
    public async Task<IActionResult> PerformanceScorecards(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new PerformanceScorecardsQuery(), cancellationToken));

    /// <summary>Department / business-unit scorecards — audits + findings rolled up the OrgUnit tree.</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("org-units")]
    public async Task<IActionResult> OrgUnitScorecards(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new OrgUnitScorecardsQuery(), cancellationToken));

    /// <summary>Budget-vs-actual per audit — budgeted hours vs logged time (P0-B).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("budget-vs-actual")]
    public async Task<IActionResult> BudgetVsActual(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new BudgetVsActualQuery(), cancellationToken));

    /// <summary>Utilisation per auditor — total logged hours split by activity category (P0-B).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("utilisation")]
    public async Task<IActionResult> Utilisation(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new UtilisationByUserQuery(), cancellationToken));

    /// <summary>Enterprise risk heatmap — open risks bucketed by their current likelihood×impact cell (P1-A).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("risk-heatmap")]
    public async Task<IActionResult> RiskHeatmap(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new RiskHeatmapQuery(), cancellationToken));

    /// <summary>Risk-register roll-up — totals + open risks by band / status / category / strategy (P1-A).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("risk-summary")]
    public async Task<IActionResult> RiskSummary(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new RiskRegisterSummaryQuery(), cancellationToken));

    /// <summary>Control-effectiveness roll-up — active controls by effectiveness + type (P1-B).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("control-effectiveness")]
    public async Task<IActionResult> ControlEffectiveness(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ControlEffectivenessQuery(), cancellationToken));

    /// <summary>Compliance-by-regulation — linked + open findings per active regulation (P1-B).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("compliance-by-regulation")]
    public async Task<IActionResult> ComplianceByRegulation(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ComplianceByRegulationQuery(), cancellationToken));

    /// <summary>Finding follow-up — management-response coverage, reopen count + verification outcomes (P2-B).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("finding-followup")]
    public async Task<IActionResult> FindingFollowUp(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new FindingFollowUpQuery(), cancellationToken));

    /// <summary>Execution-procedure coverage + sampling error-rate (P2-C).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("procedures")]
    public async Task<IActionResult> Procedures(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ProcedureSummaryQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("recurrence-clusters")]
    public async Task<IActionResult> RecurrenceClusters([FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetRecurrenceClustersQuery(cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("recurrence-clusters/{id:guid}")]
    public async Task<IActionResult> RecurrenceClusterDetail(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetRecurrenceClusterDetailQuery(id), cancellationToken));

    /// <summary>KPI time-series from the daily snapshot fact table (defaults to the trailing 90 days).</summary>
    [RequirePermission(PermissionKeys.ViewAnalytics)]
    [HttpGet("trend")]
    public async Task<IActionResult> Trend(
        [FromQuery] string metric, [FromQuery] string? dimension,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetMetricTrendQuery(metric, dimension, from, to), cancellationToken));

    /// <summary>Forces an immediate KPI snapshot for today (also captured daily by the background job).</summary>
    [RequirePermission(PermissionKeys.ConfigureDashboards)]
    [HttpPost("snapshots/capture")]
    public async Task<IActionResult> CaptureSnapshot(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CaptureAnalyticsSnapshotCommand(), cancellationToken));
}
