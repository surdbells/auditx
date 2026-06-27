using AuditX.Domain.Common;

namespace AuditX.Domain.Notifications;

/// <summary>
/// A bank-configurable rule (M10) mapping a domain event type to recipients, channels and a template.
/// Seeded defaults ship active; the dispatcher is generic and data-driven so behaviour is configured, not coded.
/// </summary>
public sealed class NotificationRule : AggregateRoot
{
    private NotificationRule()
    {
    }

    public string EventType { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>JSON: { "type": "role|named_users|payload_derived", "value": "..." }.</summary>
    public string RecipientResolutionJson { get; private set; } = null!;

    /// <summary>JSON array of channel names, e.g. ["email"] or ["email","sms"].</summary>
    public string ChannelsJson { get; private set; } = "[\"email\"]";

    public string TemplateKey { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public bool IsSystemDefault { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static NotificationRule Create(string eventType, string name, string recipientResolutionJson, string channelsJson, string templateKey, bool isActive, bool isSystemDefault)
        => new()
        {
            EventType = Guard.NotNullOrWhiteSpace(eventType, "notification.event_type_required", "Event type is required."),
            Name = Guard.NotNullOrWhiteSpace(name, "notification.rule_name_required", "Rule name is required."),
            RecipientResolutionJson = Guard.NotNullOrWhiteSpace(recipientResolutionJson, "notification.recipient_required", "Recipient resolution is required."),
            ChannelsJson = string.IsNullOrWhiteSpace(channelsJson) ? "[\"email\"]" : channelsJson,
            TemplateKey = Guard.NotNullOrWhiteSpace(templateKey, "notification.template_key_required", "Template key is required."),
            IsActive = isActive,
            IsSystemDefault = isSystemDefault,
        };

    public void Update(string name, string recipientResolutionJson, string channelsJson, string templateKey)
    {
        Name = Guard.NotNullOrWhiteSpace(name, "notification.rule_name_required", "Rule name is required.");
        RecipientResolutionJson = Guard.NotNullOrWhiteSpace(recipientResolutionJson, "notification.recipient_required", "Recipient resolution is required.");
        ChannelsJson = string.IsNullOrWhiteSpace(channelsJson) ? ChannelsJson : channelsJson;
        TemplateKey = Guard.NotNullOrWhiteSpace(templateKey, "notification.template_key_required", "Template key is required.");
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
