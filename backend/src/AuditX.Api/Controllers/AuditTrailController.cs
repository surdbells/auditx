using AuditX.Api.Authorization;
using AuditX.Application.AuditTrail.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/audit-trail")]
public sealed class AuditTrailController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewAuditTrail)]
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery(Name = "actor_user_id")] Guid? actorUserId,
        [FromQuery(Name = "event_type")] string? eventType,
        [FromQuery(Name = "target_object_type")] string? targetObjectType,
        [FromQuery(Name = "target_object_id")] Guid? targetObjectId,
        [FromQuery(Name = "date_from")] DateTimeOffset? dateFrom,
        [FromQuery(Name = "date_to")] DateTimeOffset? dateTo,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(
            new QueryAuditTrailQuery(actorUserId, eventType, targetObjectType, targetObjectId, dateFrom, dateTo, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAuditTrail)]
    [HttpGet("object/{objectType}/{objectId:guid}")]
    public async Task<IActionResult> ObjectHistory(
        string objectType, Guid objectId, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetObjectHistoryQuery(objectType, objectId, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ExportAuditTrail)]
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery(Name = "actor_user_id")] Guid? actorUserId,
        [FromQuery(Name = "event_type")] string? eventType,
        [FromQuery(Name = "target_object_type")] string? targetObjectType,
        [FromQuery(Name = "target_object_id")] Guid? targetObjectId,
        [FromQuery(Name = "date_from")] DateTimeOffset? dateFrom,
        [FromQuery(Name = "date_to")] DateTimeOffset? dateTo,
        CancellationToken cancellationToken)
    {
        var export = await dispatcher.Query(
            new ExportAuditTrailQuery(actorUserId, eventType, targetObjectType, targetObjectId, dateFrom, dateTo), cancellationToken);
        Response.Headers["X-Content-SHA256"] = export.Sha256;
        Response.Headers["X-Row-Count"] = export.RowCount.ToString();
        return File(export.Content, export.ContentType, export.FileName);
    }
}
