using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Configuration.Commands;
using AuditX.Application.Configuration.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Bank template &amp; workflow configuration (M12). A generic versioned config store: draft → activate → rollback,
/// one active version per domain, maker-checker-gated activation. Reads require ViewConfig; mutations require
/// ManageConfiguration. The one fully-wired domain in this slice is <c>exception_defaults</c>.
/// </summary>
[Authorize]
public sealed class ConfigurationsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewConfig)]
    [HttpGet("api/v1/configurations/{domain}")]
    public async Task<IActionResult> GetActive(string domain, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetActiveConfigurationQuery(domain), cancellationToken));

    [RequirePermission(PermissionKeys.ViewConfig)]
    [HttpGet("api/v1/configurations/{domain}/versions")]
    public async Task<IActionResult> ListVersions(string domain, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetConfigurationVersionsQuery(domain, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost("api/v1/configurations/{domain}")]
    public async Task<IActionResult> CreateDraft(string domain, [FromBody] CreateConfigurationDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new CreateConfigurationDraftCommand(domain, request.DefinitionJson, request.ChangeReason), cancellationToken);
        return result.PendingActionId is { } pendingId ? Accepted(new { pendingActionId = pendingId }) : Created(result.Version);
    }

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost("api/v1/configurations/{domain}/versions/{n:int}/activate")]
    public async Task<IActionResult> Activate(string domain, int n, [FromBody] ActivateConfigurationRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ActivateConfigurationVersionCommand(domain, n, request.ChangeReason), cancellationToken);
        return result.PendingActionId is { } pendingId ? Accepted(new { pendingActionId = pendingId }) : Envelope(result.Version);
    }

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost("api/v1/configurations/{domain}/rollback")]
    public async Task<IActionResult> Rollback(string domain, [FromBody] RollbackConfigurationRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new RollbackConfigurationCommand(domain, request.ToVersionNumber, request.ChangeReason), cancellationToken);
        return result.PendingActionId is { } pendingId ? Accepted(new { pendingActionId = pendingId }) : Envelope(result.Version);
    }
}
