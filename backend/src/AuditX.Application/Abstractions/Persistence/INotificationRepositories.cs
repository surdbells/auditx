using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;

namespace AuditX.Application.Abstractions.Persistence;

public interface INotificationRuleRepository
{
    Task<NotificationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRule>> GetActiveByEventTypeAsync(string eventType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRule>> ListAsync(CancellationToken cancellationToken = default);

    void Add(NotificationRule rule);
}

public interface INotificationTemplateRepository
{
    /// <summary>Resolve a template by key + channel, preferring a bank override over the system default.</summary>
    Task<NotificationTemplate?> ResolveAsync(string templateKey, NotificationChannel channel, CancellationToken cancellationToken = default);

    Task<NotificationTemplate?> GetByKeyChannelScopeAsync(string templateKey, NotificationChannel channel, TemplateScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken cancellationToken = default);

    void Add(NotificationTemplate template);
}

public interface INotificationDispatchRepository
{
    Task<NotificationDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CursorPage<NotificationDispatch>> SearchAsync(DispatchStatus? status, string? eventType, Guid? recipientUserId, PageRequest page, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDispatch>> GetDueForRetryAsync(DateTimeOffset asOf, int max, CancellationToken cancellationToken = default);

    /// <summary>Idempotency guard: has a dispatch already been created for this (event, rule, recipient, channel)?</summary>
    Task<bool> ExistsAsync(Guid eventId, Guid? ruleId, Guid? recipientUserId, NotificationChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a new dispatch row as the idempotency claim BEFORE sending. Returns false when a unique-index
    /// violation shows another worker already claimed this (event, rule, recipient, channel) — a benign duplicate
    /// to skip. Detaches the entity on any failure so the context stays usable; rethrows non-uniqueness errors.
    /// </summary>
    Task<bool> TryClaimAsync(NotificationDispatch dispatch, CancellationToken cancellationToken = default);

    void Add(NotificationDispatch dispatch);
}
