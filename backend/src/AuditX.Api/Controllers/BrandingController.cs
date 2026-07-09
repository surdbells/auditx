using AuditX.Application.Administration.Queries;
using AuditX.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// Public branding surface. Anonymous by design so the SPA shell and the login screen can apply the
/// organisation's name, theme colours and logo before a session exists. Exposes no sensitive data.
/// </summary>
[AllowAnonymous]
[Route("api/v1/branding")]
public sealed class BrandingController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetBrandingQuery(), cancellationToken));
}
