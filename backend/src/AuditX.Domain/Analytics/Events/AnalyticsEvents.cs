using AuditX.Domain.Common;

namespace AuditX.Domain.Analytics.Events;

public abstract record AnalyticsEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Raised when a recurrence cluster newly crosses the detection threshold (M9, G6): ≥3 CLOSED exceptions
/// (Cancelled excluded) for the same (auditable entity, category) within the configured window. The M10
/// post-commit dispatcher derives <c>recurrence_cluster_detected</c> and routes it to the seeded rule
/// (role Audit Manager). Carries the grouping keys + count so the email body renders without re-loading.
/// </summary>
public sealed record RecurrenceClusterDetectedEvent(
    Guid RecurrenceClusterId,
    Guid AuditableEntityId,
    string? Category,
    int ClosedExceptionCount,
    int WindowMonths) : AnalyticsEvent;
