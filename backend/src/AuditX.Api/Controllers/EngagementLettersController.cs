using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Engagement.Records;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// The auditee's side of the engagement letter. Addressed by the letter rather than the audit: acting on the audit
/// itself is closed to its auditee (independence filter), while acknowledging its letter is theirs alone — the handler
/// refuses anyone else.
/// </summary>
[Authorize]
[Route("api/v1/engagement-letters")]
public sealed class EngagementLettersController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewAudit)]
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeEngagementLetterRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new AcknowledgeEngagementLetterCommand(id, request.Comment, request.LetterVersion), cancellationToken));
}
