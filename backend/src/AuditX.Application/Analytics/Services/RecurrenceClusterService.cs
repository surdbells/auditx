using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Analytics;
using AuditX.Domain.Configuration;

namespace AuditX.Application.Analytics.Services;

/// <summary>
/// Detects exception recurrence (M9, G6). Invoked daily by <c>RecurrenceClusterScanJob</c>. Groups CLOSED exceptions
/// (Cancelled excluded) by (auditable entity, category) with ≥ the effective threshold members within the effective
/// window, then upserts one <see cref="RecurrenceCluster"/> per group. A NEWLY-detected cluster raises
/// <c>RecurrenceClusterDetectedEvent</c> (the post-commit dispatcher → M10 notification rule). The caller persists +
/// records the trail. M9 never writes the per-exception is_recurrence flag — M6 owns it.
///
/// <para>M12: the window and detection threshold are read from the active <c>exception_defaults</c> bank-configuration
/// version via <see cref="IActiveConfigurationProvider"/> (same source as the raise-time path, G6). The
/// <see cref="WindowMonths"/> / <see cref="RecurrenceCluster.DetectionThreshold"/> constants remain the fallback when
/// no active config exists. <see cref="RecurrenceCluster.DetectionThreshold"/> stays a domain const (purity); the
/// service reads the effective threshold from config and compares against it explicitly.</para>
/// </summary>
public sealed class RecurrenceClusterService(IRecurrenceClusterRepository clusters, IActiveConfigurationProvider activeConfig, IClock clock)
{
    /// <summary>Default recurrence window in months (fallback when no active exception_defaults config exists).</summary>
    public const int WindowMonths = 24;

    /// <summary>
    /// Run a detection pass. Mutates the change tracker (adds new clusters / refreshes existing) but does NOT
    /// save — the job saves so the new aggregates' events dispatch post-commit. Returns the number of clusters
    /// that newly reached the threshold this run (for the trail).
    /// </summary>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // Read the effective window + threshold from the active exception_defaults config (G6); fall back to the
        // domain constants when no active version exists, reproducing the pre-M12 behaviour exactly.
        var config = activeConfig.GetActive<ExceptionDefaultsDefinition>(ConfigurationDomains.ExceptionDefaults);
        var windowMonths = config?.RecurrenceWindowMonths ?? WindowMonths;
        var threshold = config?.RecurrenceThreshold ?? RecurrenceCluster.DetectionThreshold;

        // ALL windowed (entity, category) groups (minCount 1), not just qualifying ones, so a tracked cluster that
        // has fallen below the threshold (members closed > window ago) is revisited and downgraded, and a later
        // re-cross is re-detected. A brand-new cluster is still only CREATED when it reaches the threshold.
        var groups = await clusters.GetClosedRecurrenceGroupsAsync(windowMonths, 1, now, cancellationToken);

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
                var wasBelow = existing.ClosedExceptionCount < threshold;
                existing.Refresh(group.MemberExceptionIds, group.FirstClosedAt, group.LastClosedAt, windowMonths, threshold, now);
                if (wasBelow && existing.ClosedExceptionCount >= threshold)
                {
                    newlyDetected++; // re-crossed the threshold → re-notify (handled inside Refresh)
                }
            }
            else if (group.MemberExceptionIds.Count >= threshold)
            {
                var cluster = RecurrenceCluster.Create(
                    group.AuditableEntityId, group.Category, windowMonths,
                    group.MemberExceptionIds, group.FirstClosedAt, group.LastClosedAt, threshold, now);
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
                existing.Refresh([], existing.FirstOccurredAt, existing.LastOccurredAt, windowMonths, threshold, now);
            }
        }

        return newlyDetected;
    }

    private static string NormaliseCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
}
