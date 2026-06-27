using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Notifications.Commands;
using AuditX.Application.Notifications.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1")]
public sealed class NotificationsController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpGet("admin/events/catalogue")]
    public async Task<IActionResult> EventCatalogue(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetEventCatalogueQuery(), cancellationToken));

    // ---- Rules ----

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpGet("notification-rules")]
    public async Task<IActionResult> ListRules(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListNotificationRulesQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpPost("notification-rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateNotificationRuleRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateNotificationRuleCommand(
            request.EventType, request.Name, request.RecipientResolutionJson, request.ChannelsJson, request.TemplateKey, request.IsActive), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpPatch("notification-rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateNotificationRuleRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateNotificationRuleCommand(
            id, request.Name, request.RecipientResolutionJson, request.ChannelsJson, request.TemplateKey, request.IsActive, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpDelete("notification-rules/{id:guid}")]
    public async Task<IActionResult> DeactivateRule(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeactivateNotificationRuleCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpPost("notification-rules/preview")]
    public async Task<IActionResult> PreviewRule([FromBody] PreviewNotificationRuleRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new PreviewNotificationRuleQuery(request.RecipientResolutionJson, request.TemplateKey, request.SamplePayloadJson), cancellationToken));

    // ---- Templates ----

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpGet("notification-templates")]
    public async Task<IActionResult> ListTemplates(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListNotificationTemplatesQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpPost("notification-templates")]
    public async Task<IActionResult> OverrideTemplate([FromBody] NotificationTemplateRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CreateOrOverrideTemplateCommand(request.TemplateKey, request.Channel, request.SubjectTemplate, request.BodyTemplate), cancellationToken));

    // ---- Dispatch log + dead-letter ----

    [RequirePermission(PermissionKeys.ConfigureNotifications)]
    [HttpGet("notification-dispatches")]
    public async Task<IActionResult> ListDispatches(
        [FromQuery] string? status, [FromQuery] string? eventType, [FromQuery] Guid? recipient, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListDispatchesQuery(status, eventType, recipient, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.AdminOps)]
    [HttpGet("notification-dispatches/dead-letter")]
    public async Task<IActionResult> ListDeadLetter([FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListDeadLetterQuery(cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.AdminOps)]
    [HttpPost("notification-dispatches/{id:guid}/retry")]
    public async Task<IActionResult> RetryDispatch(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RetryDispatchCommand(id), cancellationToken));

    // Per-user preferences live on UsersController (GET/PATCH /users/me/notification-preferences) from M1.
}
