using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Notifications;

/// <summary>
/// A Scriban subject/body template (M10), keyed by (template_key, channel, scope). A bank-scoped row
/// overrides the system default at render time. The resolved version is pinned onto each dispatch.
/// </summary>
public sealed class NotificationTemplate : AggregateRoot
{
    private NotificationTemplate()
    {
    }

    public string TemplateKey { get; private set; } = null!;

    public NotificationChannel Channel { get; private set; }

    public TemplateScope Scope { get; private set; }

    public string? SubjectTemplate { get; private set; }

    public string BodyTemplate { get; private set; } = null!;

    public int Version { get; private set; } = 1;

    public byte[] RowVersion { get; private set; } = [];

    public static NotificationTemplate Create(string templateKey, NotificationChannel channel, TemplateScope scope, string? subjectTemplate, string bodyTemplate)
        => new()
        {
            TemplateKey = Guard.NotNullOrWhiteSpace(templateKey, "notification.template_key_required", "Template key is required."),
            Channel = channel,
            Scope = scope,
            SubjectTemplate = subjectTemplate,
            BodyTemplate = Guard.NotNullOrWhiteSpace(bodyTemplate, "notification.template_body_required", "Template body is required."),
        };

    public void UpdateContent(string? subjectTemplate, string bodyTemplate)
    {
        SubjectTemplate = subjectTemplate;
        BodyTemplate = Guard.NotNullOrWhiteSpace(bodyTemplate, "notification.template_body_required", "Template body is required.");
        Version++;
    }
}
