using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.ReferenceData;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// The generic managed reference-data store (audit types, exception categories, …). Reads are authenticated-only so
/// any signed-in user can populate a dropdown; writes require <see cref="PermissionKeys.ManageConfiguration"/>.
/// </summary>
[Authorize]
[Route("api/v1/reference-data")]
public sealed class ReferenceDataController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReferenceDataCategoriesQuery(), cancellationToken));

    [HttpGet("{category}")]
    public async Task<IActionResult> List(string category, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReferenceDataQuery(category, includeInactive), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost("{category}")]
    public async Task<IActionResult> Create(string category, [FromBody] CreateReferenceDataItemRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateReferenceDataItemCommand(category, request.Code, request.Label, request.Description, request.SortOrder), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPatch("{category}/{id:guid}")]
    public async Task<IActionResult> Update(string category, Guid id, [FromBody] UpdateReferenceDataItemRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateReferenceDataItemCommand(id, request.Label, request.Description, request.SortOrder), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost("{category}/{id:guid}/archive")]
    public async Task<IActionResult> Archive(string category, Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ArchiveReferenceDataItemCommand(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageConfiguration)]
    [HttpPost("{category}/{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(string category, Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReactivateReferenceDataItemCommand(id), cancellationToken));
}
