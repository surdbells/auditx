using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Reports.Commands;
using AuditX.Application.Reports.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>M8 bank-configurable report templates: list (ViewConfig), create version + activate (ConfigureReports).</summary>
[Authorize]
public sealed class ReportTemplatesController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewConfig)]
    [HttpGet("api/v1/report-templates")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReportTemplatesQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureReports)]
    [HttpPost("api/v1/report-templates")]
    public async Task<IActionResult> Create([FromBody] CreateReportTemplateRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateReportTemplateCommand(request.Name, request.TemplateDefinition), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureReports)]
    [HttpPatch("api/v1/report-templates/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateReportTemplateRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ActivateReportTemplateCommand(id, request.Reason), cancellationToken));
}
