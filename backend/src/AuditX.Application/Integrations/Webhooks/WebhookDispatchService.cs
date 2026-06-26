using System.Security.Cryptography;
using System.Text;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Integrations;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;

namespace AuditX.Application.Integrations.Webhooks;

/// <summary>
/// Dispatches domain events to subscribed webhooks: signs each payload with HMAC-SHA-256, records a
/// <see cref="WebhookDelivery"/>, and on failure schedules retries with exponential backoff before
/// dead-lettering (US-M14-009/010/011). The retry sweep is driven by a background job.
/// </summary>
public sealed class WebhookDispatchService(
    IWebhookRepository webhooks,
    ICredentialProtector protector,
    IWebhookSender sender,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const int DefaultTimeoutSeconds = 30;

    /// <summary>Create and attempt deliveries for all active subscriptions to the event.</summary>
    public async Task DispatchAsync(string eventType, Guid eventId, string payloadJson, CancellationToken cancellationToken = default)
    {
        var subscriptions = await webhooks.GetActiveSubscriptionsForEventAsync(eventType, cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        foreach (var subscription in subscriptions)
        {
            var delivery = WebhookDelivery.Create(subscription.Id, eventType, eventId, payloadJson);
            webhooks.AddDelivery(delivery);
            await AttemptAsync(delivery, subscription, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Attempt all deliveries whose retry time is due (invoked by the retry job).</summary>
    public async Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default)
    {
        var due = await webhooks.GetDueForRetryAsync(clock.UtcNow, limit: 100, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        foreach (var delivery in due)
        {
            var subscription = await webhooks.GetSubscriptionAsync(delivery.SubscriptionId, cancellationToken);
            if (subscription is null)
            {
                continue;
            }

            await AttemptAsync(delivery, subscription, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AttemptAsync(WebhookDelivery delivery, WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        var secret = protector.Unprotect(subscription.EncryptedHmacSecret);
        var signature = Sign(delivery.PayloadJson, secret);

        var result = await sender.SendAsync(subscription.DestinationUrl, delivery.PayloadJson, signature, DefaultTimeoutSeconds, cancellationToken);
        if (result.Success)
        {
            delivery.RecordSuccess(clock.UtcNow);
        }
        else
        {
            delivery.RecordFailure(clock.UtcNow, result.Error ?? "delivery failed");
            if (delivery.Status == WebhookDeliveryStatus.DeadLetter)
            {
                audit.RecordAs(ActorType.System, "webhook-dispatcher", null,
                    AuditEventTypes.WebhookDeadLettered, AuditTargetTypes.WebhookDelivery, delivery.Id,
                    payload: new { delivery.EventType, delivery.SubscriptionId, delivery.LastError });
            }
        }
    }

    public static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
