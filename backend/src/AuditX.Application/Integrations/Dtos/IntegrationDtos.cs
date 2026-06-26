namespace AuditX.Application.Integrations.Dtos;

/// <summary>Integration configuration. Credentials are never returned — only whether they are set.</summary>
public sealed record IntegrationDto(
    Guid Id,
    string Type,
    string Name,
    string ConnectionDetailsJson,
    bool HasCredentials,
    int TimeoutSeconds,
    Guid? FallbackIntegrationId,
    bool IsPrimary,
    bool IsActive);

public sealed record IntegrationHealthDto(
    Guid IntegrationId,
    string State,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    int RecentFailureCount);

public sealed record IntegrationTestResultDto(bool Success, string Detail);

public sealed record WebhookSubscriptionDto(
    Guid Id,
    string DestinationUrl,
    IReadOnlyList<string> SubscribedEventTypes,
    bool IsActive);

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid SubscriptionId,
    string EventType,
    Guid EventId,
    string Status,
    int Attempts,
    DateTimeOffset? NextRetryAt,
    string? LastError,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset CreatedAt);
