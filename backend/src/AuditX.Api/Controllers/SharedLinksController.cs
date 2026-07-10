using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Sharing.Commands;
using AuditX.Application.Sharing.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Shareable report links (D3-B). A link is an opaque, revocable, optionally-expiring reference — never an access
/// grant. Creating, listing and resolving a link all require <c>ViewReport</c> AND pass the report's own resource
/// scope (ReportAccess), so a link only resolves for a viewer who could already open the report. Resolving returns
/// only the target descriptor (type + id), never content; the caller then opens the report through its own endpoint.
/// </summary>
[Authorize]
public sealed class SharedLinksController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpPost("api/v1/reports/{reportId:guid}/share")]
    public async Task<IActionResult> Create(Guid reportId, [FromBody] CreateShareLinkRequest? request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateReportShareLinkCommand(reportId, request?.ExpiresInDays), cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/reports/{reportId:guid}/share")]
    public async Task<IActionResult> ListForReport(Guid reportId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReportShareLinksQuery(reportId), cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpGet("api/v1/shared-links/{slug}")]
    public async Task<IActionResult> Resolve(string slug, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ResolveSharedLinkQuery(slug), cancellationToken));

    [RequirePermission(PermissionKeys.ViewReport)]
    [HttpDelete("api/v1/shared-links/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RevokeSharedLinkCommand(id, version), cancellationToken);
        return NoContent();
    }
}
