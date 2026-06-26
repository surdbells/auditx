using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Planning.Commands;
using AuditX.Application.Planning.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/annual-plans")]
public sealed class AnnualPlansController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewPlan)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListPlansQuery(status, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewPlan)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetPlanQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewPlan)]
    [HttpGet("{id:guid}/execution")]
    public async Task<IActionResult> Execution(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new PlanExecutionQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreatePlanCommand(request.PeriodLabel, request.PeriodStart, request.PeriodEnd), cancellationToken));

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdatePlanCommand(id, request.PeriodLabel, request.PeriodStart, request.PeriodEnd), cancellationToken));

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddPlanItemRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new AddPlanItemCommand(id, request.EntityId, request.AuditType, request.PlannedStartDate, request.PlannedEndDate, request.EstimatedEffortDays, request.AssignedLeadUserId), cancellationToken));

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RemovePlanItemCommand(id, itemId), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SubmitPlanCommand(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpPost("{id:guid}/submit-revision")]
    public async Task<IActionResult> SubmitRevision(Guid id, [FromBody] SubmitPlanRevisionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SubmitPlanRevisionCommand(id, request.Kind, request.ItemId, request.NewStartDate, request.NewEndDate), cancellationToken));

    [RequirePermission(PermissionKeys.AcChair)]
    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decision(Guid id, [FromBody] PlanDecisionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RecordPlanDecisionCommand(id, request.Decision, request.Detail, request.Comments), cancellationToken));

    [RequirePermission(PermissionKeys.ManagePlan)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ClosePlanCommand(id), cancellationToken);
        return NoContent();
    }
}

[Authorize]
[Route("api/v1/coverage-analytics")]
public sealed class CoverageAnalyticsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewCoverage)]
    [HttpGet("not-audited-since")]
    public async Task<IActionResult> NotAuditedSince([FromQuery] int months, [FromQuery] string? entityType, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new Application.Coverage.Queries.NotAuditedSinceQuery(months, entityType), cancellationToken));

    [RequirePermission(PermissionKeys.ViewCoverage)]
    [HttpGet("high-risk-gaps")]
    public async Task<IActionResult> HighRiskGaps([FromQuery] int months, [FromQuery] string? entityType, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new Application.Coverage.Queries.HighRiskGapsQuery(months, entityType), cancellationToken));

    [RequirePermission(PermissionKeys.ViewCoverage)]
    [HttpGet("matrix")]
    public async Task<IActionResult> Matrix([FromQuery] int window, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new Application.Coverage.Queries.CoverageMatrixQuery(window), cancellationToken));
}
