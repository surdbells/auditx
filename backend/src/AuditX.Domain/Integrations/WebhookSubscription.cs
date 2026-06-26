using AuditX.Domain.Common;

namespace AuditX.Domain.Integrations;

/// <summary>
/// A subscription that POSTs domain events to a bank-internal endpoint (M14). The destination must be
/// on the bank's internal network (validated before persistence). Payloads are signed with HMAC-SHA-256
/// using a per-subscription secret stored only as ciphertext.
/// </summary>
public sealed class WebhookSubscription : AggregateRoot, ISoftDeletable
{
    private readonly List<string> _subscribedEventTypes = [];

    private WebhookSubscription()
    {
    }

    public string DestinationUrl { get; private set; } = null!;

    public IReadOnlyList<string> SubscribedEventTypes => _subscribedEventTypes.AsReadOnly();

    public byte[] EncryptedHmacSecret { get; private set; } = null!;

    public string? RetryPolicyJson { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static WebhookSubscription Create(string destinationUrl, IEnumerable<string> subscribedEventTypes, byte[] encryptedHmacSecret, string? retryPolicyJson)
    {
        var subscription = new WebhookSubscription
        {
            DestinationUrl = Guard.NotNullOrWhiteSpace(destinationUrl, "webhook.url_required", "Destination URL is required."),
            EncryptedHmacSecret = encryptedHmacSecret ?? throw new DomainException("webhook.secret_required", "An HMAC secret is required."),
            RetryPolicyJson = retryPolicyJson,
            IsActive = true,
        };
        subscription._subscribedEventTypes.AddRange(subscribedEventTypes.Distinct());
        if (subscription._subscribedEventTypes.Count == 0)
        {
            throw new DomainException("webhook.no_events", "At least one event type must be subscribed.");
        }

        return subscription;
    }

    public void Deactivate() => IsActive = false;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
        IsActive = false;
    }
}
