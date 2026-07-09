using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Commands;
using AuditX.Application.Reports.Queries;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
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

    [RequirePermission(PermissionKeys.GenerateReport)]
    [HttpPost("api/v1/reports/standalone")]
    public async Task<IActionResult> GenerateStandalone([FromBody] GenerateStandaloneReportRequest request, CancellationToken cancellationToken)
    {
        var kind = ParseStandaloneKind(request.Kind);
        var result = await dispatcher.Send(new GenerateStandaloneReportCommand(kind, request.Docx ?? false), cancellationToken);
        return Accepted(result);
    }

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/standalone")]
    public async Task<IActionResult> ListStandalone([FromQuery] string? kind, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        ReportKind? kindFilter = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            kindFilter = ParseStandaloneKind(kind);
        }

        return Envelope(await dispatcher.Query(new ListStandaloneReportsQuery(kindFilter, cursor, limit), cancellationToken));
    }

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
            new DistributeReportCommand(id, request.RecipientUserIds ?? [], request.RecipientEmailAddresses ?? [], request.RecipientRoleNames),
            cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/{id:guid}/distributions")]
    public async Task<IActionResult> Distributions(Guid id, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReportDistributionsQuery(id, cursor, limit), cancellationToken));

    /// <summary>Parses a standalone report kind, rejecting an unrecognised value or the engagement kind (400).</summary>
    private static ReportKind ParseStandaloneKind(string? value)
    {
        if (!EnumExtensions.TryParseSnake<ReportKind>(value, out var kind) || kind == ReportKind.AuditEngagement)
        {
            throw new DomainException("report.invalid_kind",
                "Kind must be one of: executive_summary, annual_plan_status, kpi_pack.");
        }

        return kind;
    }
}
