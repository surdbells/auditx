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

    public async Task<CursorPage<WebhookDelivery>> GetDeliveriesAsync(WebhookDeliveryStatus? status, Guid? subscriptionId, PageRequest page, CancellationToken cancellationToken = default)
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

        if (TryDecodeCursor(page.Cursor, out var afterCreated, out var afterId))
        {
            // Keyset: rows strictly older than the cursor in the (created_at desc, id desc) total order.
            query = query.Where(d => d.CreatedAt < afterCreated || (d.CreatedAt == afterCreated && d.Id.CompareTo(afterId) < 0));
        }

        var items = await query
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore ? EncodeCursor(items[^1].CreatedAt, items[^1].Id) : null;
        return new CursorPage<WebhookDelivery>(items, next, hasMore);
    }

    private static string EncodeCursor(DateTimeOffset createdAt, Guid id)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{createdAt.UtcTicks}:{id}"));

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':', 2);
            if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParse(parts[1], out id)
                && ticks >= DateTimeOffset.MinValue.UtcTicks && ticks <= DateTimeOffset.MaxValue.UtcTicks)
            {
                createdAt = new DateTimeOffset(ticks, TimeSpan.Zero);
                return true;
            }
        }
        catch (FormatException)
        {
            // Malformed cursor → treat as no cursor (first page).
        }

        return false;
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
