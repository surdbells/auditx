namespace AuditX.Api.Contracts;

public sealed record CreateNotificationRuleRequest(string EventType, string Name, string RecipientResolutionJson, string ChannelsJson, string TemplateKey, bool IsActive);

public sealed record UpdateNotificationRuleRequest(string Name, string RecipientResolutionJson, string ChannelsJson, string TemplateKey, bool IsActive, string Version);

public sealed record NotificationTemplateRequest(string TemplateKey, string Channel, string? SubjectTemplate, string BodyTemplate);

public sealed record PreviewNotificationRuleRequest(string RecipientResolutionJson, string TemplateKey, string SamplePayloadJson);
