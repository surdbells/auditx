using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Commands;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Systemic root-cause gaps (P2): weaknesses that surface across multiple findings, tracked with their own
/// open→closed lifecycle and linked to the contributing exceptions. Reads require ViewExceptions; mutations
/// require ManageException.
/// </summary>
[Authorize]
[Route("api/v1/root-cause-gaps")]
public sealed class RootCauseGapsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListRootCauseGapsQuery(status, search, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewExceptions)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetRootCauseGapQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRootCauseGapRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateRootCauseGapCommand(request.Title, request.Description, request.Category, request.OwnerUserId, request.TargetDate), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRootCauseGapRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateRootCauseGapCommand(id, request.Title, request.Description, request.Category, request.OwnerUserId, request.TargetDate, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseRootCauseGapRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CloseRootCauseGapCommand(id, request.Rationale, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReopenRootCauseGapCommand(id, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpPost("{id:guid}/exceptions")]
    public async Task<IActionResult> LinkException(Guid id, [FromBody] LinkExceptionToGapRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new LinkExceptionToGapCommand(id, request.ExceptionId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ManageException)]
    [HttpDelete("{id:guid}/exceptions/{exceptionId:guid}")]
    public async Task<IActionResult> UnlinkException(Guid id, Guid exceptionId, [FromQuery] string version, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UnlinkExceptionFromGapCommand(id, exceptionId, version), cancellationToken));
}
