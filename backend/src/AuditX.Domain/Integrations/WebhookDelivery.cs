using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Integrations;

/// <summary>
/// Tracks the delivery of one event to one subscription, including retry scheduling and the dead-letter
/// outcome (US-M14-010/011). Retry backoff (1m, 5m, 15m, 1h) is applied by the dispatcher.
/// </summary>
public sealed class WebhookDelivery : Entity
{
    /// <summary>Exponential backoff schedule; index by attempt number (1-based). Beyond the last entry → dead-letter.</summary>
    public static readonly IReadOnlyList<TimeSpan> Backoff =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1),
    ];

    private WebhookDelivery()
    {
    }

    public Guid SubscriptionId { get; private set; }

    public string EventType { get; private set; } = null!;

    public Guid EventId { get; private set; }

    public string PayloadJson { get; private set; } = null!;

    public WebhookDeliveryStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextRetryAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public static WebhookDelivery Create(Guid subscriptionId, string eventType, Guid eventId, string payloadJson)
        => new()
        {
            SubscriptionId = subscriptionId,
            EventType = eventType,
            EventId = eventId,
            PayloadJson = payloadJson,
            Status = WebhookDeliveryStatus.Pending,
            Attempts = 0,
        };

    public void RecordSuccess(DateTimeOffset atUtc)
    {
        Attempts++;
        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAt = atUtc;
        NextRetryAt = null;
        LastError = null;
    }

    /// <summary>Record a failed attempt; schedules the next retry or dead-letters once attempts are exhausted.</summary>
    public void RecordFailure(DateTimeOffset atUtc, string error)
    {
        Attempts++;
        LastError = error;
        if (Attempts >= Backoff.Count)
        {
            Status = WebhookDeliveryStatus.DeadLetter;
            NextRetryAt = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
            NextRetryAt = atUtc + Backoff[Attempts];
        }
    }

    /// <summary>Re-queue a dead-lettered delivery for a manual retry (US-M14-011).</summary>
    public void Requeue()
    {
        Status = WebhookDeliveryStatus.Pending;
        NextRetryAt = null;
    }
}
