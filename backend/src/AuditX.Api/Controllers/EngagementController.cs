using AuditX.Api.Authorization;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Engagement.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Engagement Lifecycle (the audit journey navigator): the portfolio board of the caller's engagements
/// and the per-engagement lifecycle journey, each surfacing the caller's role-scoped next best action.
/// </summary>
[Authorize]
[Route("api/v1/engagements")]
public sealed class EngagementController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewEngagementLifecycle)]
    [HttpGet]
    public async Task<IActionResult> Board([FromQuery] string? status, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListMyEngagementsQuery(status), cancellationToken));

    [RequirePermission(PermissionKeys.ViewEngagementLifecycle)]
    [HttpGet("{id:guid}/journey")]
    public async Task<IActionResult> Journey(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetEngagementJourneyQuery(id), cancellationToken));
}
