using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Administration;
using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class IntegrationRepository(AppDbContext db) : IIntegrationRepository
{
    public Task<IntegrationConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Integrations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<IntegrationConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.Integrations.OrderBy(i => i.Type).ToListAsync(cancellationToken);

    public Task<IntegrationConfiguration?> GetActiveAuthProviderAsync(CancellationToken cancellationToken = default)
        => db.Integrations.FirstOrDefaultAsync(
            i => i.IsActive && (i.Type == IntegrationType.ActiveDirectory || i.Type == IntegrationType.Saml || i.Type == IntegrationType.Oidc),
            cancellationToken);

    public Task<IntegrationHealthStatus?> GetHealthAsync(Guid integrationId, CancellationToken cancellationToken = default)
        => db.IntegrationHealth.FirstOrDefaultAsync(h => h.IntegrationId == integrationId, cancellationToken);

    public void Add(IntegrationConfiguration integration) => db.Integrations.Add(integration);

    public void AddHealth(IntegrationHealthStatus health) => db.IntegrationHealth.Add(health);
}

public sealed class WebhookRepository(AppDbContext db) : IWebhookRepository
{
    public Task<WebhookSubscription?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
        => db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsForEventAsync(string eventType, CancellationToken cancellationToken = default)
        => await db.WebhookSubscriptions
            .Where(s => s.IsActive && s.SubscribedEventTypes.Contains(eventType))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WebhookSubscription>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default)
        => await db.WebhookSubscriptions.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);

    public Task<WebhookDelivery?> GetDeliveryAsync(Guid id, CancellationToken cancellationToken = default)
        => db.WebhookDeliveries.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<WebhookDelivery>> GetDeliveriesAsync(WebhookDeliveryStatus? status, Guid? subscriptionId, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = db.WebhookDeliveries.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(d => d.Status == s);
        }

        if (subscriptionId is { } sub)
        {
            query = query.Where(d => d.SubscriptionId == sub);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<WebhookDelivery>(items, total, page.Page, page.PageSize);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetDueForRetryAsync(DateTimeOffset asOfUtc, int limit, CancellationToken cancellationToken = default)
        => await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Failed && d.NextRetryAt != null && d.NextRetryAt <= asOfUtc)
            .OrderBy(d => d.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void AddSubscription(WebhookSubscription subscription) => db.WebhookSubscriptions.Add(subscription);

    public void AddDelivery(WebhookDelivery delivery) => db.WebhookDeliveries.Add(delivery);
}

public sealed class AdministrationRepository(AppDbContext db) : IAdministrationRepository
{
    public Task<SupportChannelSession?> GetLatestSupportSessionAsync(CancellationToken cancellationToken = default)
        => db.SupportChannelSessions.OrderByDescending(s => s.EnabledAt).FirstOrDefaultAsync(cancellationToken);

    public Task<SupportChannelSession?> GetActiveSupportSessionAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
        => db.SupportChannelSessions
            .Where(s => s.RevokedAt == null && s.ExpiresAt > asOfUtc)
            .OrderByDescending(s => s.EnabledAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddSupportSession(SupportChannelSession session) => db.SupportChannelSessions.Add(session);

    public async Task<IReadOnlyList<ReleaseInstall>> GetReleasesAsync(int limit, CancellationToken cancellationToken = default)
        => await db.ReleaseInstalls.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync(cancellationToken);

    public void AddRelease(ReleaseInstall release) => db.ReleaseInstalls.Add(release);

    public async Task<IReadOnlyList<RestoreDrill>> GetRestoreDrillsAsync(int limit, CancellationToken cancellationToken = default)
        => await db.RestoreDrills.AsNoTracking().OrderByDescending(d => d.ExecutedAt).Take(limit).ToListAsync(cancellationToken);

    public void AddRestoreDrill(RestoreDrill drill) => db.RestoreDrills.Add(drill);

    public Task<ObjectRestoreRequest?> GetObjectRestoreAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ObjectRestoreRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void AddObjectRestore(ObjectRestoreRequest request) => db.ObjectRestoreRequests.Add(request);
}
