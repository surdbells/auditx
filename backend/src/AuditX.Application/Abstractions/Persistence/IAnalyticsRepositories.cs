using AuditX.Application.Common.Models;
using AuditX.Domain.Analytics;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>One candidate recurrence group: closed exceptions for a (entity, category) within the window.</summary>
public sealed record RecurrenceGroup(
    Guid AuditableEntityId,
    string? Category,
    IReadOnlyList<Guid> MemberExceptionIds,
    DateTimeOffset FirstClosedAt,
    DateTimeOffset LastClosedAt);

/// <summary>One member exception inside a recurrence cluster (drilldown projection).</summary>
public sealed record RecurrenceMemberProjection(
    Guid ExceptionId,
    Guid AuditId,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ClosedAt);

/// <summary>Persistence + grouping port for M9 recurrence clusters (used by the daily scan and the drilldown queries).</summary>
public interface IRecurrenceClusterRepository
{
    /// <summary>
    /// Group CLOSED exceptions (Cancelled excluded) by (auditable_entity_id, category) with ≥ <paramref name="minCount"/>
    /// members within <paramref name="windowMonths"/> by closed_at. Only entities (non-null id) participate.
    /// </summary>
    Task<IReadOnlyList<RecurrenceGroup>> GetClosedRecurrenceGroupsAsync(
        int windowMonths, int minCount, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurrenceCluster>> ListTrackedAsync(CancellationToken cancellationToken = default);

    Task<CursorPage<RecurrenceCluster>> ListPagedAsync(PageRequest page, CancellationToken cancellationToken = default);

    Task<RecurrenceCluster?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Project the member exceptions of a cluster from the stored id list (drilldown).</summary>
    Task<IReadOnlyList<RecurrenceMemberProjection>> GetMembersAsync(IReadOnlyList<Guid> exceptionIds, CancellationToken cancellationToken = default);

    void Add(RecurrenceCluster cluster);
}
