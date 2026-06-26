namespace AuditX.Api.Contracts;

public sealed record CreateIntegrationRequest(
    string Type, string Name, string? ConnectionDetailsJson, string? Credentials, int TimeoutSeconds, Guid? FallbackIntegrationId);

public sealed record UpdateIntegrationRequest(
    string Name, string? ConnectionDetailsJson, string? Credentials, int TimeoutSeconds, Guid? FallbackIntegrationId, bool IsActive);

public sealed record CreateWebhookRequest(
    string DestinationUrl, IReadOnlyList<string> SubscribedEventTypes, string HmacSecret, string? RetryPolicyJson);
