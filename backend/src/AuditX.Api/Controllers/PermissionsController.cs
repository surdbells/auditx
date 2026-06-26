using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/permissions")]
public sealed class PermissionsController(IDispatcher dispatcher) : ApiControllerBase
{
    /// <summary>The full, machine-readable permission catalogue (US-M1-014).</summary>
    [HttpGet("catalogue")]
    public async Task<IActionResult> Catalogue(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetPermissionCatalogueQuery(), cancellationToken));
}
