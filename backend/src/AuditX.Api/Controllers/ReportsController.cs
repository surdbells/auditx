using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Commands;
using AuditX.Application.Reports.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// M8 report generation, retrieval, download (verify-on-read), hash verification and distribution. There is NO
/// mutating verb on <c>/reports/{id}</c> — a completed report is immutable (US-M8-014); regeneration is a new
/// version via POST on the audit. Audit-scope is enforced in-handler; the attributes gate the permission globally.
/// </summary>
[Authorize]
public sealed class ReportsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.GenerateReport)]
    [HttpPost("api/v1/audits/{auditId:guid}/reports")]
    public async Task<IActionResult> Generate(Guid auditId, [FromBody] GenerateReportRequest? request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new GenerateReportCommand(auditId, request?.Docx ?? false), cancellationToken);
        return Accepted(result);
    }

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/audits/{auditId:guid}/reports")]
    public async Task<IActionResult> ListForAudit(Guid auditId, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAuditReportsQuery(auditId, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetReportQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] string format, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Query(new DownloadReportArtefactQuery(id, format), cancellationToken);
        Response.Headers["X-Report-Sha256"] = result.Sha256Hash;
        return File(result.Content, result.ContentType, result.Filename);
    }

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/{id:guid}/verify-hash")]
    public async Task<IActionResult> VerifyHash(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new VerifyReportHashQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.DistributeReport)]
    [HttpPost("api/v1/reports/{id:guid}/distribute")]
    public async Task<IActionResult> Distribute(Guid id, [FromBody] DistributeReportRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(
            new DistributeReportCommand(id, request.RecipientUserIds ?? [], request.RecipientEmailAddresses ?? []), cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/{id:guid}/distributions")]
    public async Task<IActionResult> Distributions(Guid id, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReportDistributionsQuery(id, cursor, limit), cancellationToken));
}
