using System.Text.Json;
using AuditX.Domain.Analytics.Events;
using AuditX.Domain.Common;

namespace AuditX.Domain.Analytics;

/// <summary>
/// A detected recurrence cluster (M9, G6): ≥<see cref="DetectionThreshold"/> CLOSED exceptions (Cancelled excluded)
/// for the same (<see cref="AuditableEntityId"/>, <see cref="Category"/>) within <see cref="WindowMonths"/>. The daily
/// scan upserts one row per (entity, category) and raises <see cref="RecurrenceClusterDetectedEvent"/> only when a
/// cluster NEWLY reaches the threshold (created at/above it, or refreshed across the &lt;3 → ≥3 boundary). M9 does NOT
/// write the per-exception is_recurrence flag — M6 owns it (avoids a double-writer).
/// </summary>
public sealed class RecurrenceCluster : AggregateRoot
{
    /// <summary>
    /// Default minimum closed exceptions for a cluster to be a recurrence. Kept as a domain const for purity and used
    /// as the fallback; the M9 scan reads the effective threshold from the active <c>exception_defaults</c> config
    /// (M12) and passes it into <see cref="Create"/> / <see cref="Refresh"/>.
    /// </summary>
    public const int DetectionThreshold = 3;

    private RecurrenceCluster()
    {
    }

    public Guid AuditableEntityId { get; private set; }

    public string? Category { get; private set; }

    public int ClosedExceptionCount { get; private set; }

    public int WindowMonths { get; private set; }

    public DateTimeOffset FirstOccurredAt { get; private set; }

    public DateTimeOffset LastOccurredAt { get; private set; }

    public DateTimeOffset DetectedAt { get; private set; }

    public DateTimeOffset? NotifiedAt { get; private set; }

    /// <summary>JSON array of the member exception ids that make up this cluster.</summary>
    public string MemberExceptionIdsJson { get; private set; } = "[]";

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<Guid> MemberExceptionIds =>
        JsonSerializer.Deserialize<List<Guid>>(MemberExceptionIdsJson) ?? [];

    /// <summary>
    /// Create a brand-new cluster. Raises the detected event when it is created at/above <paramref name="threshold"/>
    /// (the common case — the scan only persists clusters that meet the rule). The threshold defaults to
    /// <see cref="DetectionThreshold"/> when not supplied.
    /// </summary>
    public static RecurrenceCluster Create(
        Guid auditableEntityId, string? category, int windowMonths,
        IReadOnlyList<Guid> memberExceptionIds, DateTimeOffset firstOccurredAt, DateTimeOffset lastOccurredAt,
        int threshold, DateTimeOffset nowUtc)
    {
        var cluster = new RecurrenceCluster
        {
            AuditableEntityId = auditableEntityId,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            WindowMonths = windowMonths,
            FirstOccurredAt = firstOccurredAt,
            LastOccurredAt = lastOccurredAt,
            DetectedAt = nowUtc,
            ClosedExceptionCount = memberExceptionIds.Count,
            MemberExceptionIdsJson = Serialize(memberExceptionIds),
        };

        if (cluster.ClosedExceptionCount >= threshold)
        {
            cluster.MarkDetected(nowUtc);
        }

        return cluster;
    }

    /// <summary>
    /// Refresh an existing cluster with a recomputed member set. Raises the detected event only when the count
    /// crosses from below <paramref name="threshold"/> to at/above it (a newly-detected recurrence), so re-runs are
    /// idempotent.
    /// </summary>
    public void Refresh(
        IReadOnlyList<Guid> memberExceptionIds, DateTimeOffset firstOccurredAt, DateTimeOffset lastOccurredAt,
        int windowMonths, int threshold, DateTimeOffset nowUtc)
    {
        var wasBelowThreshold = ClosedExceptionCount < threshold;

        WindowMonths = windowMonths;
        FirstOccurredAt = firstOccurredAt;
        LastOccurredAt = lastOccurredAt;
        ClosedExceptionCount = memberExceptionIds.Count;
        MemberExceptionIdsJson = Serialize(memberExceptionIds);

        if (wasBelowThreshold && ClosedExceptionCount >= threshold)
        {
            MarkDetected(nowUtc);
        }
    }

    private void MarkDetected(DateTimeOffset nowUtc)
    {
        DetectedAt = nowUtc;
        NotifiedAt = nowUtc;
        RaiseDomainEvent(new RecurrenceClusterDetectedEvent(Id, AuditableEntityId, Category, ClosedExceptionCount, WindowMonths));
    }

    private static string Serialize(IReadOnlyList<Guid> ids) => JsonSerializer.Serialize(ids);
}
