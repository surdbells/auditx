using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Notifications;

/// <summary>
/// The outcome + retry record for delivering one notification to one recipient over one channel (M10).
/// Mirrors the M14 <c>WebhookDelivery</c> retry/dead-letter state machine (backoff 1m/5m/15m/1h).
/// </summary>
public sealed class NotificationDispatch : AggregateRoot
{
    /// <summary>Backoff schedule indexed by attempt number; beyond the last entry → dead-letter.</summary>
    public static readonly IReadOnlyList<TimeSpan> Backoff =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1),
    ];

    private NotificationDispatch()
    {
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = null!;

    public Guid? RuleId { get; private set; }

    public Guid? RecipientUserId { get; private set; }

    public string RecipientAddress { get; private set; } = null!;

    public NotificationChannel Channel { get; private set; }

    public string TemplateKey { get; private set; } = null!;

    public int TemplateVersion { get; private set; }

    public string? RenderedSubject { get; private set; }

    public string RenderedBody { get; private set; } = null!;

    public string? Severity { get; private set; }

    public DispatchStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextRetryAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? ProviderResponseJson { get; private set; }

    public string? LastError { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static NotificationDispatch Create(
        Guid eventId, string eventType, Guid? ruleId, Guid? recipientUserId, string recipientAddress,
        NotificationChannel channel, string templateKey, int templateVersion, string? renderedSubject, string renderedBody, string? severity)
        => new()
        {
            EventId = eventId,
            EventType = eventType,
            RuleId = ruleId,
            RecipientUserId = recipientUserId,
            RecipientAddress = Guard.NotNullOrWhiteSpace(recipientAddress, "notification.recipient_address_required", "A recipient address is required."),
            Channel = channel,
            TemplateKey = templateKey,
            TemplateVersion = templateVersion,
            RenderedSubject = renderedSubject,
            RenderedBody = Guard.NotNullOrWhiteSpace(renderedBody, "notification.body_required", "A rendered body is required."),
            Severity = severity,
            Status = DispatchStatus.Pending,
            Attempts = 0,
        };

    public void RecordDelivered(DateTimeOffset atUtc, string? providerMessageId, string? providerResponseJson)
    {
        Attempts++;
        Status = DispatchStatus.Delivered;
        DispatchedAt = atUtc;
        DeliveredAt = atUtc;
        ProviderMessageId = providerMessageId;
        ProviderResponseJson = providerResponseJson;
        NextRetryAt = null;
        LastError = null;
    }

    /// <summary>Record a failed attempt; schedules the next retry, or dead-letters when permanent or exhausted.</summary>
    public void RecordFailure(DateTimeOffset atUtc, string error, bool isPermanent)
    {
        // Index the backoff by the PRE-increment attempt so the first retry honours Backoff[0] (1m) and all
        // four entries are used: failure 1→+1m, 2→+5m, 3→+15m, 4→+1h, 5→dead-letter.
        var attemptIndex = Attempts;
        Attempts++;
        LastError = error;
        if (isPermanent || attemptIndex >= Backoff.Count)
        {
            Status = DispatchStatus.DeadLetter;
            NextRetryAt = null;
        }
        else
        {
            Status = DispatchStatus.Failed;
            NextRetryAt = atUtc + Backoff[attemptIndex];
        }
    }

    /// <summary>Manual re-queue of a dead-lettered (or failed) dispatch; attempts reset so the next try starts at Backoff[0].</summary>
    public void Requeue()
    {
        Status = DispatchStatus.Pending;
        Attempts = 0;
        NextRetryAt = null;
        LastError = null;
    }
}
