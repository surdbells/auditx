using AuditX.Application.Common.Enums;
using AuditX.Application.Integrations.Dtos;
using AuditX.Domain.Integrations;

namespace AuditX.Application.Integrations.Mapping;

public static class IntegrationMappings
{
    // Enum outputs are snake_case (ToSnake) to match the project-wide API contract and the persisted column
    // values (the Type/State/Status columns are stored snake_case via SnakeCaseEnumConverter).
    public static IntegrationDto ToDto(this IntegrationConfiguration integration) => new(
        integration.Id,
        integration.Type.ToSnake(),
        integration.Name,
        integration.ConnectionDetailsJson,
        integration.EncryptedCredentials is { Length: > 0 },
        integration.TimeoutSeconds,
        integration.FallbackIntegrationId,
        integration.IsPrimary,
        integration.IsActive);

    public static IntegrationHealthDto ToDto(this IntegrationHealthStatus health) => new(
        health.IntegrationId, health.State.ToSnake(), health.LastSuccessAt, health.LastFailureAt, health.RecentFailureCount);

    public static WebhookSubscriptionDto ToDto(this WebhookSubscription subscription) => new(
        subscription.Id, subscription.DestinationUrl, subscription.SubscribedEventTypes.ToArray(), subscription.IsActive);

    public static WebhookDeliveryDto ToDto(this WebhookDelivery delivery) => new(
        delivery.Id, delivery.SubscriptionId, delivery.EventType, delivery.EventId, delivery.Status.ToSnake(),
        delivery.Attempts, delivery.NextRetryAt, delivery.LastError, delivery.DeliveredAt, delivery.CreatedAt);
}
