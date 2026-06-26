using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;

namespace AuditX.Domain.Tests.Integrations;

public sealed class WebhookDeliveryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Failures_schedule_backoff_then_dead_letter()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), "exception_raised", Guid.NewGuid(), "{}");

        // Backoff has 4 entries; the 4th failure dead-letters.
        for (var i = 0; i < WebhookDelivery.Backoff.Count - 1; i++)
        {
            delivery.RecordFailure(Now, "boom");
            Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
            Assert.NotNull(delivery.NextRetryAt);
        }

        delivery.RecordFailure(Now, "boom");
        Assert.Equal(WebhookDeliveryStatus.DeadLetter, delivery.Status);
        Assert.Null(delivery.NextRetryAt);
    }

    [Fact]
    public void Success_marks_delivered()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), "e", Guid.NewGuid(), "{}");
        delivery.RecordSuccess(Now);
        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(Now, delivery.DeliveredAt);
    }

    [Fact]
    public void Requeue_resets_dead_letter_to_pending()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), "e", Guid.NewGuid(), "{}");
        for (var i = 0; i < WebhookDelivery.Backoff.Count; i++)
        {
            delivery.RecordFailure(Now, "x");
        }

        Assert.Equal(WebhookDeliveryStatus.DeadLetter, delivery.Status);
        delivery.Requeue();
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
    }
}

public sealed class IntegrationHealthTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Health_transitions_through_degraded_to_failing_and_alerts_once()
    {
        var health = IntegrationHealthStatus.Create(Guid.NewGuid());

        var alerted = false;
        for (var i = 0; i < IntegrationHealthStatus.FailingThreshold; i++)
        {
            alerted |= health.RecordFailure(Now);
        }

        Assert.Equal(IntegrationHealthState.Failing, health.State);
        Assert.True(alerted);

        // Already failing → no new alert.
        Assert.False(health.RecordFailure(Now));
    }

    [Fact]
    public void Success_resets_health()
    {
        var health = IntegrationHealthStatus.Create(Guid.NewGuid());
        for (var i = 0; i < IntegrationHealthStatus.DegradedThreshold; i++)
        {
            health.RecordFailure(Now);
        }

        Assert.Equal(IntegrationHealthState.Degraded, health.State);
        health.RecordSuccess(Now);
        Assert.Equal(IntegrationHealthState.Healthy, health.State);
        Assert.Equal(0, health.RecentFailureCount);
    }
}
