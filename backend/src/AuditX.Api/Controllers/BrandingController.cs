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
    {
        // Never cache: branding + page-guide visibility + resource limits are admin-editable and must take
        // effect on the next load. Without this, a browser's heuristic caching can serve stale values for a
        // while, so a settings change (e.g. toggling the Overview/Walkthrough buttons) only "sticks" after
        // several reloads.
        Response.Headers.CacheControl = "no-store";
        return Envelope(await dispatcher.Query(new GetBrandingQuery(), cancellationToken));
    }
}
