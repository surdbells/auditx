using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Scheduling.Commands;
using AuditX.Application.Scheduling.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Recurring report schedules (D3-C). A schedule auto-generates a standalone (cross-audit) report on a cadence and
/// emails it to a set of recipients; the runner background job executes due schedules as the System actor. Managing
/// schedules requires <c>ScheduleReports</c> (Audit Manager / Administrator) — it is a bank-wide automation, not a
/// per-audit act, so there is no resource scope beyond the permission.
/// </summary>
[Authorize]
public sealed class ReportSchedulesController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ScheduleReports)]
    [HttpGet("api/v1/report-schedules")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReportSchedulesQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ScheduleReports)]
    [HttpGet("api/v1/report-schedules/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetReportScheduleQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ScheduleReports)]
    [HttpPost("api/v1/report-schedules")]
    public async Task<IActionResult> Create([FromBody] CreateReportScheduleRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(
            new CreateReportScheduleCommand(request.Name, request.Kind, request.Cadence, request.RecipientUserIds ?? [], request.RecipientEmails ?? []),
            cancellationToken));

    [RequirePermission(PermissionKeys.ScheduleReports)]
    [HttpPatch("api/v1/report-schedules/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReportScheduleRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(
            new UpdateReportScheduleCommand(id, request.Name, request.Cadence, request.RecipientUserIds ?? [], request.RecipientEmails ?? [], request.IsActive, request.Version),
            cancellationToken));

    [RequirePermission(PermissionKeys.ScheduleReports)]
    [HttpDelete("api/v1/report-schedules/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteReportScheduleCommand(id, version), cancellationToken);
        return NoContent();
    }
}
