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
