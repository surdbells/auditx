using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Integrations.Commands;
using AuditX.Application.Integrations.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/integrations")]
public sealed class IntegrationsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewIntegrations)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListIntegrationsQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ViewIntegrations)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetIntegrationQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewIntegrationHealth)]
    [HttpGet("{id:guid}/health")]
    public async Task<IActionResult> Health(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetIntegrationHealthQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureIntegrations)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateIntegrationCommand(
            request.Type, request.Name, request.ConnectionDetailsJson, request.Credentials, request.TimeoutSeconds, request.FallbackIntegrationId), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureIntegrations)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntegrationRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateIntegrationCommand(
            id, request.Name, request.ConnectionDetailsJson, request.Credentials, request.TimeoutSeconds, request.FallbackIntegrationId, request.IsActive), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureIntegrations)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeactivateIntegrationCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ConfigureIntegrations)]
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new TestIntegrationCommand(id), cancellationToken));
}
