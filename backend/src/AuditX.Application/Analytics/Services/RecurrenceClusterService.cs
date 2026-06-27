using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Analytics;

namespace AuditX.Application.Analytics.Services;

/// <summary>
/// Detects exception recurrence (M9, G6). Invoked daily by <c>RecurrenceClusterScanJob</c>. Groups CLOSED exceptions
/// (Cancelled excluded) by (auditable entity, category) with ≥<see cref="RecurrenceCluster.DetectionThreshold"/> members
/// within the window, then upserts one <see cref="RecurrenceCluster"/> per group. A NEWLY-detected cluster raises
/// <c>RecurrenceClusterDetectedEvent</c> (the post-commit dispatcher → M10 notification rule). The caller persists +
/// records the trail. M9 never writes the per-exception is_recurrence flag — M6 owns it.
/// </summary>
public sealed class RecurrenceClusterService(IRecurrenceClusterRepository clusters, IClock clock)
{
    /// <summary>Recurrence window in months (constant for now; M12-config later).</summary>
    public const int WindowMonths = 24;

    /// <summary>
    /// Run a detection pass. Mutates the change tracker (adds new clusters / refreshes existing) but does NOT
    /// save — the job saves so the new aggregates' events dispatch post-commit. Returns the number of clusters
    /// that newly reached the threshold this run (for the trail).
    /// </summary>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // ALL windowed (entity, category) groups (minCount 1), not just qualifying ones, so a tracked cluster that
        // has fallen below the threshold (members closed > window ago) is revisited and downgraded, and a later
        // re-cross is re-detected. A brand-new cluster is still only CREATED when it reaches the threshold.
        var groups = await clusters.GetClosedRecurrenceGroupsAsync(WindowMonths, 1, now, cancellationToken);

        var tracked = await clusters.ListTrackedAsync(cancellationToken);
        var byKey = tracked.ToDictionary(c => (c.AuditableEntityId, NormaliseCategory(c.Category)));
        var seenKeys = new HashSet<(Guid, string)>();

        var newlyDetected = 0;
        foreach (var group in groups)
        {
            var key = (group.AuditableEntityId, NormaliseCategory(group.Category));
            seenKeys.Add(key);
            if (byKey.TryGetValue(key, out var existing))
            {
                var wasBelow = existing.ClosedExceptionCount < RecurrenceCluster.DetectionThreshold;
                existing.Refresh(group.MemberExceptionIds, group.FirstClosedAt, group.LastClosedAt, WindowMonths, now);
                if (wasBelow && existing.ClosedExceptionCount >= RecurrenceCluster.DetectionThreshold)
                {
                    newlyDetected++; // re-crossed the threshold → re-notify (handled inside Refresh)
                }
            }
            else if (group.MemberExceptionIds.Count >= RecurrenceCluster.DetectionThreshold)
            {
                var cluster = RecurrenceCluster.Create(
                    group.AuditableEntityId, group.Category, WindowMonths,
                    group.MemberExceptionIds, group.FirstClosedAt, group.LastClosedAt, now);
                clusters.Add(cluster);
                newlyDetected++;
            }
            // else: a brand-new sub-threshold group — not a recurrence yet, so it is not persisted.
        }

        // A tracked cluster whose group has no closed exceptions left in the window (all aged out): downgrade it to
        // zero so it drops out of the active list. The row is retained (not deleted) so a future re-cross re-detects.
        foreach (var existing in tracked)
        {
            var key = (existing.AuditableEntityId, NormaliseCategory(existing.Category));
            if (!seenKeys.Contains(key))
            {
                existing.Refresh([], existing.FirstOccurredAt, existing.LastOccurredAt, WindowMonths, now);
            }
        }

        return newlyDetected;
    }

    private static string NormaliseCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
}
