using AuditX.Domain.Administration;
using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;

namespace AuditX.Application.Abstractions.Persistence;

public interface IIntegrationRepository
{
    Task<IntegrationConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IntegrationConfiguration?> GetActiveAuthProviderAsync(CancellationToken cancellationToken = default);

    Task<IntegrationHealthStatus?> GetHealthAsync(Guid integrationId, CancellationToken cancellationToken = default);

    void Add(IntegrationConfiguration integration);

    void AddHealth(IntegrationHealthStatus health);
}

public interface IWebhookRepository
{
    Task<WebhookSubscription?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsForEventAsync(string eventType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookSubscription>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task<WebhookDelivery?> GetDeliveryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookDelivery>> GetDeliveriesAsync(WebhookDeliveryStatus? status, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookDelivery>> GetDueForRetryAsync(DateTimeOffset asOfUtc, int limit, CancellationToken cancellationToken = default);

    void AddSubscription(WebhookSubscription subscription);

    void AddDelivery(WebhookDelivery delivery);
}

public interface IAdministrationRepository
{
    Task<SupportChannelSession?> GetLatestSupportSessionAsync(CancellationToken cancellationToken = default);

    Task<SupportChannelSession?> GetActiveSupportSessionAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);

    void AddSupportSession(SupportChannelSession session);

    Task<IReadOnlyList<ReleaseInstall>> GetReleasesAsync(int limit, CancellationToken cancellationToken = default);

    void AddRelease(ReleaseInstall release);

    Task<IReadOnlyList<RestoreDrill>> GetRestoreDrillsAsync(int limit, CancellationToken cancellationToken = default);

    void AddRestoreDrill(RestoreDrill drill);

    Task<ObjectRestoreRequest?> GetObjectRestoreAsync(Guid id, CancellationToken cancellationToken = default);

    void AddObjectRestore(ObjectRestoreRequest request);
}
