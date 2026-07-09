using AuditX.Application.Common.Messaging;
using AuditX.Application.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Cross-module quick search for the header. Authenticated-only (no <c>[RequirePermission]</c>): the handler
/// resolves the caller's effective permissions and only returns hits from modules they are allowed to view.
/// </summary>
[Authorize]
[Route("api/v1/search")]
public sealed class SearchController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new SearchQuery(q), cancellationToken));
}
