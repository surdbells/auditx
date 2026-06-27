namespace AuditX.Application.Notifications.Dtos;

public sealed record NotificationRuleDto(
    Guid Id, string EventType, string Name, string RecipientResolutionJson, string ChannelsJson,
    string TemplateKey, bool IsActive, bool IsSystemDefault, string Version);

public sealed record NotificationTemplateDto(
    Guid Id, string TemplateKey, string Channel, string Scope, string? SubjectTemplate, string BodyTemplate, int Version);

public sealed record NotificationDispatchDto(
    Guid Id, Guid EventId, string EventType, Guid? RuleId, Guid? RecipientUserId, string RecipientAddress,
    string Channel, string TemplateKey, int TemplateVersion, string? RenderedSubject, string? Severity,
    string Status, int Attempts, DateTimeOffset? NextRetryAt, DateTimeOffset? DeliveredAt, string? LastError);

public sealed record RulePreviewDto(IReadOnlyList<string> ResolvedRecipientAddresses, string? RenderedSubject, string RenderedBody);
