using AuditX.Application.Abstractions;
using AuditX.Domain.Audits;
using AuditX.Domain.Common;
using AuditX.Domain.Evidence;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Universe;
using AuditX.Infrastructure.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditX.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps the audit columns (created/updated at/by) on every tracked entity before save, and dispatches
/// the domain events raised by aggregate roots after the transaction commits. It also drops the cached permissions
/// of anyone the save NAMES on something, so the access that involvement brings is there on their next request.
/// </summary>
public sealed class AuditingSaveChangesInterceptor(
    IClock clock, ICurrentUser currentUser, IDomainEventDispatcher dispatcher, PermissionCache permissionCache)
    : SaveChangesInterceptor
{
    /// <summary>Users named by the save in flight — collected before it (entry states are still known), used after.</summary>
    private readonly HashSet<Guid> _namedUsers = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            PromoteAggregateRootsOnChildChange(eventData.Context);
            StampAuditColumns(eventData.Context);
            CollectNamedUsers(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await InvalidateNamedUsersAsync();
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// The people an added or changed row names: an audit's auditee and lead, a team member, a finding's or action's
    /// owner, the person a document is requested from, an area's owner, a root-cause gap's or remediation's owner.
    /// Being named is what gives someone the baseline abilities (<c>BaselineAccess</c>), so their cached permissions
    /// must not outlive the save. Over-invalidating is harmless (one cache delete); missing one would leave a newly
    /// named person locked out for the cache's lifetime.
    /// </summary>
    private void CollectNamedUsers(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            Guid?[] named = entry.Entity switch
            {
                Audit audit => [audit.AuditeeUserId, audit.LeadUserId],
                AuditTeamMember member => [member.UserId],
                AuditException exception => [exception.OwnerUserId],
                MapAction action => [action.OwnerUserId],
                EvidenceRequest request => [request.RequestedFromUserId],
                AuditableEntity area => [area.OwnerUserId],
                RootCauseGap gap => [gap.OwnerUserId],
                RootCauseGapRemediation remediation => [remediation.OwnerUserId],
                _ => [],
            };

            foreach (var userId in named)
            {
                if (userId is { } id && id != Guid.Empty)
                {
                    _namedUsers.Add(id);
                }
            }
        }
    }

    private async Task InvalidateNamedUsersAsync()
    {
        if (_namedUsers.Count == 0)
        {
            return;
        }

        var users = _namedUsers.ToArray();
        _namedUsers.Clear();
        foreach (var userId in users)
        {
            try
            {
                await permissionCache.InvalidateUserAsync(userId);
            }
            catch (StackExchange.Redis.RedisException)
            {
                // The data is already committed; a cache that cannot be reached must not fail the request. The entry
                // expires on its own within the cache TTL.
            }
        }
    }

    private void StampAuditColumns(DbContext context)
    {
        var now = clock.UtcNow;
        var actor = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
            }
        }
    }

    /// <summary>
    /// When a child entity inside an aggregate is added/modified/removed but the root row's scalars are
    /// untouched (e.g. adding a checklist item or team member), EF would leave the root Unchanged and skip
    /// its rowversion concurrency check. Promote the owning root to Modified so its optimistic-concurrency
    /// token participates and child-only edits cannot silently overwrite each other (US-M4-006).
    /// </summary>
    private static void PromoteAggregateRootsOnChildChange(DbContext context)
    {
        var changedRootIds = context.ChangeTracker.Entries<IBelongsToAggregate>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.AggregateRootId)
            .ToHashSet();

        if (changedRootIds.Count == 0)
        {
            return;
        }

        foreach (var rootEntry in context.ChangeTracker.Entries<AggregateRoot>())
        {
            if (rootEntry.State == EntityState.Unchanged && changedRootIds.Contains(rootEntry.Entity.Id))
            {
                rootEntry.State = EntityState.Modified;
            }
        }
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToArray();

        if (aggregates.Length == 0)
        {
            return;
        }

        var events = aggregates.SelectMany(a => a.DomainEvents).ToArray();
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        await dispatcher.DispatchAsync(events, cancellationToken);
    }
}
