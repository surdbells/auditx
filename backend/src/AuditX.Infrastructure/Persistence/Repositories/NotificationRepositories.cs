using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class NotificationRuleRepository(AppDbContext db) : INotificationRuleRepository
{
    public Task<NotificationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<NotificationRule>> GetActiveByEventTypeAsync(string eventType, CancellationToken cancellationToken = default)
        => await db.NotificationRules.AsNoTracking().Where(r => r.EventType == eventType && r.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationRule>> ListAsync(CancellationToken cancellationToken = default)
        => await db.NotificationRules.AsNoTracking().OrderBy(r => r.EventType).ThenBy(r => r.Name).ToListAsync(cancellationToken);

    public void Add(NotificationRule rule) => db.NotificationRules.Add(rule);
}

public sealed class NotificationTemplateRepository(AppDbContext db) : INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> ResolveAsync(string templateKey, NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        // Prefer a bank override over the system default.
        var candidates = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.TemplateKey == templateKey && t.Channel == channel)
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(t => t.Scope == TemplateScope.Bank) ?? candidates.FirstOrDefault(t => t.Scope == TemplateScope.System);
    }

    public Task<NotificationTemplate?> GetByKeyChannelScopeAsync(string templateKey, NotificationChannel channel, TemplateScope scope, CancellationToken cancellationToken = default)
        => db.NotificationTemplates.FirstOrDefaultAsync(t => t.TemplateKey == templateKey && t.Channel == channel && t.Scope == scope, cancellationToken);

    public async Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken cancellationToken = default)
        => await db.NotificationTemplates.AsNoTracking().OrderBy(t => t.TemplateKey).ThenBy(t => t.Channel).ToListAsync(cancellationToken);

    public void Add(NotificationTemplate template) => db.NotificationTemplates.Add(template);
}

public sealed class NotificationDispatchRepository(AppDbContext db) : INotificationDispatchRepository
{
    public Task<NotificationDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.NotificationDispatches.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<NotificationDispatch>> SearchAsync(DispatchStatus? status, string? eventType, Guid? recipientUserId, PageSpec page, CancellationToken cancellationToken = default)
    {
        var query = db.NotificationDispatches.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(d => d.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(d => d.EventType == eventType);
        }

        if (recipientUserId is { } rid)
        {
            query = query.Where(d => d.RecipientUserId == rid);
        }

        return await query.OrderBy(d => d.Id).ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDispatch>> GetDueForRetryAsync(DateTimeOffset asOf, int max, CancellationToken cancellationToken = default)
        => await db.NotificationDispatches
            .Where(d => d.Status == DispatchStatus.Failed && d.NextRetryAt != null && d.NextRetryAt <= asOf)
            .OrderBy(d => d.NextRetryAt)
            .Take(max)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid eventId, Guid? ruleId, Guid? recipientUserId, NotificationChannel channel, CancellationToken cancellationToken = default)
        => db.NotificationDispatches.AsNoTracking().AnyAsync(
            d => d.EventId == eventId && d.RuleId == ruleId && d.RecipientUserId == recipientUserId && d.Channel == channel, cancellationToken);

    public async Task<bool> TryClaimAsync(NotificationDispatch dispatch, CancellationToken cancellationToken = default)
    {
        db.NotificationDispatches.Add(dispatch);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (PersistenceErrors.IsUniqueViolation(ex))
        {
            db.Entry(dispatch).State = EntityState.Detached;
            return false; // another worker already created this dispatch — benign duplicate.
        }
        catch
        {
            db.Entry(dispatch).State = EntityState.Detached; // keep the context usable for sibling dispatches.
            throw;
        }
    }

    public void Add(NotificationDispatch dispatch) => db.NotificationDispatches.Add(dispatch);
}
