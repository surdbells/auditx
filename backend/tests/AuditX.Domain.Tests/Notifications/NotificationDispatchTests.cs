using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;

namespace AuditX.Domain.Tests.Notifications;

public sealed class NotificationDispatchTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static NotificationDispatch NewDispatch() => NotificationDispatch.Create(
        Guid.NewGuid(), "exception_raised", Guid.NewGuid(), Guid.NewGuid(), "owner@bank.local",
        NotificationChannel.Email, "exception_raised", templateVersion: 1, "subject", "body", "High");

    [Fact]
    public void Create_starts_pending_with_no_attempts()
    {
        var d = NewDispatch();
        Assert.Equal(DispatchStatus.Pending, d.Status);
        Assert.Equal(0, d.Attempts);
        Assert.Null(d.NextRetryAt);
    }

    [Fact]
    public void First_failure_schedules_the_one_minute_backoff()
    {
        var d = NewDispatch();
        d.RecordFailure(Now, "smtp down", isPermanent: false);

        Assert.Equal(DispatchStatus.Failed, d.Status);
        Assert.Equal(1, d.Attempts);
        Assert.Equal(Now + TimeSpan.FromMinutes(1), d.NextRetryAt);
    }

    [Fact]
    public void Backoff_uses_all_four_intervals_then_dead_letters()
    {
        var d = NewDispatch();
        var expected = new[]
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1),
        };

        foreach (var interval in expected)
        {
            d.RecordFailure(Now, "transient", isPermanent: false);
            Assert.Equal(DispatchStatus.Failed, d.Status);
            Assert.Equal(Now + interval, d.NextRetryAt);
        }

        // The fifth failure exhausts the schedule and dead-letters.
        d.RecordFailure(Now, "transient", isPermanent: false);
        Assert.Equal(DispatchStatus.DeadLetter, d.Status);
        Assert.Null(d.NextRetryAt);
    }

    [Fact]
    public void Permanent_failure_dead_letters_immediately()
    {
        var d = NewDispatch();
        d.RecordFailure(Now, "mailbox does not exist", isPermanent: true);

        Assert.Equal(DispatchStatus.DeadLetter, d.Status);
        Assert.Null(d.NextRetryAt);
    }

    [Fact]
    public void RecordDelivered_marks_delivered_and_clears_retry()
    {
        var d = NewDispatch();
        d.RecordFailure(Now, "transient", isPermanent: false);
        d.RecordDelivered(Now, "provider-123", providerResponseJson: null);

        Assert.Equal(DispatchStatus.Delivered, d.Status);
        Assert.Equal(Now, d.DeliveredAt);
        Assert.Equal("provider-123", d.ProviderMessageId);
        Assert.Null(d.NextRetryAt);
        Assert.Null(d.LastError);
    }

    [Fact]
    public void Requeue_resets_to_pending_so_the_next_try_starts_at_the_first_backoff()
    {
        var d = NewDispatch();
        d.RecordFailure(Now, "permanent", isPermanent: true); // dead-letter
        d.Requeue();

        Assert.Equal(DispatchStatus.Pending, d.Status);
        Assert.Equal(0, d.Attempts);
        Assert.Null(d.NextRetryAt);

        d.RecordFailure(Now, "transient", isPermanent: false);
        Assert.Equal(Now + TimeSpan.FromMinutes(1), d.NextRetryAt);
    }
}
