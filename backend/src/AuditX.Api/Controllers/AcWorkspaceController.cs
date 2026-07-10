using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Ac.Commands;
using AuditX.Application.Ac.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>The read-only AC dashboard (M13; ACMember). Live aggregates only — no sanctions subject identity anywhere.</summary>
[Authorize]
public sealed class AcDashboardController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.AcMember)]
    [HttpGet("api/v1/ac-dashboard")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new AcDashboardQuery(), cancellationToken));
}

/// <summary>
/// M13 Audit-Committee action items. Create (ACMember), list (ACMember/CIA), update/close (CIA; close needs a
/// response else 422) and chair acknowledgement of closure (ACChair; terminal).
/// </summary>
[Authorize]
[Route("api/v1/ac-action-items")]
public sealed class AcActionItemsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.AcMember)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAcActionItemRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(
            new CreateAcActionItemCommand(request.Title, request.Description, request.AssignedToUserId, request.DueDate), cancellationToken));

    [RequirePermission(PermissionKeys.AcMember)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAcActionItemsQuery(status, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.Cia)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAcActionItemRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateAcActionItemCommand(id, request.MarkInProgress ?? false, request.ClosureResponse), cancellationToken));

    [RequirePermission(PermissionKeys.AcChair)]
    [HttpPost("{id:guid}/acknowledge-closure")]
    public async Task<IActionResult> AcknowledgeClosure(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new AcknowledgeAcActionItemClosureCommand(id), cancellationToken));
}

/// <summary>M13 generic AC commentary (A3/BR-M13-010). Polymorphic post + get by target (ACMember).</summary>
[Authorize]
[Route("api/v1/ac-comments")]
public sealed class AcCommentsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.AcMember)]
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] AddAcCommentRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new AddAcCommentCommand(request.TargetType, request.TargetId, request.Comment), cancellationToken));

    [RequirePermission(PermissionKeys.AcMember)]
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string targetType, [FromQuery] Guid targetId, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetAcCommentsQuery(targetType, targetId), cancellationToken));
}

/// <summary>M13 restricted-finding visibility (FR-M13-009; CIA). Sets the per-finding allow-list applied per-requester at read.</summary>
[Authorize]
public sealed class FindingVisibilityController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.Cia)]
    [HttpPost("api/v1/findings/{type}/{id:guid}/restrict-visibility")]
    public async Task<IActionResult> Restrict(string type, Guid id, [FromBody] RestrictFindingVisibilityRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(
            new RestrictFindingVisibilityCommand(type, id, request.AllowedUserIds ?? [], request.Reason), cancellationToken));
}
