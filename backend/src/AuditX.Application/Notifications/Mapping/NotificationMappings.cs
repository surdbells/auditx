using AuditX.Application.Common.Enums;
using AuditX.Application.Notifications.Dtos;
using AuditX.Domain.Notifications;

namespace AuditX.Application.Notifications.Mapping;

public static class NotificationMappings
{
    public static NotificationRuleDto ToDto(this NotificationRule r) => new(
        r.Id, r.EventType, r.Name, r.RecipientResolutionJson, r.ChannelsJson, r.TemplateKey, r.IsActive, r.IsSystemDefault,
        Convert.ToBase64String(r.Version ?? []));

    public static NotificationTemplateDto ToDto(this NotificationTemplate t) => new(
        t.Id, t.TemplateKey, t.Channel.ToSnake(), t.Scope.ToSnake(), t.SubjectTemplate, t.BodyTemplate, t.Version);

    public static NotificationDispatchDto ToDto(this NotificationDispatch d) => new(
        d.Id, d.EventId, d.EventType, d.RuleId, d.RecipientUserId, d.RecipientAddress, d.Channel.ToSnake(),
        d.TemplateKey, d.TemplateVersion, d.RenderedSubject, d.Severity, d.Status.ToSnake(), d.Attempts,
        d.NextRetryAt, d.DeliveredAt, d.LastError);
}
