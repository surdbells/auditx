using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Integrations.Webhooks;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1")]
public sealed class WebhooksController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewIntegrations)]
    [HttpGet("webhook-subscriptions")]
    public async Task<IActionResult> ListSubscriptions(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListWebhookSubscriptionsQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureWebhooks)]
    [HttpPost("webhook-subscriptions")]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateWebhookRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateWebhookSubscriptionCommand(
            request.DestinationUrl, request.SubscribedEventTypes, request.HmacSecret, request.RetryPolicyJson), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureWebhooks)]
    [HttpDelete("webhook-subscriptions/{id:guid}")]
    public async Task<IActionResult> DeleteSubscription(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteWebhookSubscriptionCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ViewIntegrations)]
    [HttpGet("webhook-deliveries")]
    public async Task<IActionResult> ListDeliveries(
        [FromQuery] string? status, [FromQuery] Guid? subscriptionId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListWebhookDeliveriesQuery(status, subscriptionId, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.AdminOps)]
    [HttpPost("webhook-deliveries/{id:guid}/retry")]
    public async Task<IActionResult> RetryDelivery(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RetryWebhookDeliveryCommand(id), cancellationToken);
        return NoContent();
    }
}
