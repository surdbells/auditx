using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/maker-checker")]
public sealed class MakerCheckerController(IDispatcher dispatcher) : ApiControllerBase
{
    /// <summary>Pending actions the current user is eligible to check (excludes their own).</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> Pending([FromQuery] string? actionType, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListPendingActionsQuery(actionType), cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ApproveActionCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectActionRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RejectActionCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageBankSettings)]
    [HttpGet("gates")]
    public async Task<IActionResult> Gates(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListMakerCheckerGatesQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ManageBankSettings)]
    [HttpPut("gates")]
    public async Task<IActionResult> ConfigureGate([FromBody] ConfigureGateRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(
            new ConfigureMakerCheckerGateCommand(request.ActionType, request.IsEnabled, request.CheckerRoleName, request.AllowMakerAsChecker),
            cancellationToken));
}
