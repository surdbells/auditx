using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.SavedViews.Commands;
using AuditX.Application.SavedViews.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Saved views (D3-A): a user's named filter/parameter sets per screen, plus any shared views. These are personal
/// productivity data, so the endpoints are authentication-gated only (no business permission) and owner-scoped
/// in-handler — a non-owner may list + apply a shared view but only the owner may edit, share or delete it. Applying
/// a view only pre-fills filter controls; the underlying data stays gated by each screen's own endpoint.
/// </summary>
[Authorize]
[Route("api/v1/saved-views")]
public sealed class SavedViewsController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string viewKey, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListSavedViewsQuery(viewKey), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSavedViewRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(
            new CreateSavedViewCommand(request.ViewKey, request.Name, request.ParametersJson, request.IsShared), cancellationToken));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSavedViewRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(
            new UpdateSavedViewCommand(id, request.Name, request.ParametersJson, request.IsShared, request.Version), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteSavedViewCommand(id, version), cancellationToken);
        return NoContent();
    }
}
