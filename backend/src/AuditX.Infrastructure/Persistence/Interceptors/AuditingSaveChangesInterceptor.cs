using AuditX.Application.Abstractions;
using AuditX.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditX.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps the audit columns (created/updated at/by) on every tracked entity before save, and dispatches
/// the domain events raised by aggregate roots after the transaction commits.
/// </summary>
public sealed class AuditingSaveChangesInterceptor(IClock clock, ICurrentUser currentUser, IDomainEventDispatcher dispatcher)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            PromoteAggregateRootsOnChildChange(eventData.Context);
            StampAuditColumns(eventData.Context);
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
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
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
