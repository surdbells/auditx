using AuditX.Application.Abstractions.Analytics;
using AuditX.Domain.Analytics;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Analytics;

/// <summary>EF-backed store for the append-only <see cref="AnalyticsSnapshot"/> KPI fact table.</summary>
public sealed class AnalyticsSnapshotStore(AppDbContext db) : IAnalyticsSnapshotStore
{
    public async Task ReplaceForDateAsync(
        DateOnly asOfDate, IReadOnlyList<AnalyticsSnapshot> rows, CancellationToken cancellationToken = default)
    {
        // Delete-then-insert so a same-day re-run is idempotent (bulk delete executes immediately).
        await db.AnalyticsSnapshots.Where(s => s.AsOfDate == asOfDate).ExecuteDeleteAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        db.AnalyticsSnapshots.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MetricPoint>> GetTrendAsync(
        string metricKey, string? dimension, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var query = db.AnalyticsSnapshots.AsNoTracking()
            .Where(s => s.MetricKey == metricKey && s.AsOfDate >= from && s.AsOfDate <= to);

        query = dimension is null
            ? query.Where(s => s.Dimension == null)
            : query.Where(s => s.Dimension == dimension);

        return await query
            .OrderBy(s => s.AsOfDate)
            .Select(s => new MetricPoint(s.AsOfDate, s.Value))
            .ToListAsync(cancellationToken);
    }
}
