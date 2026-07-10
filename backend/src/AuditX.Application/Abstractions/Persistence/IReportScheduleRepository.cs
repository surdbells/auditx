using AuditX.Domain.Scheduling;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for <see cref="ReportSchedule"/> (D3-C recurring report generation).</summary>
public interface IReportScheduleRepository
{
    Task<ReportSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All live schedules, newest first (the admin console list).</summary>
    Task<IReadOnlyList<ReportSchedule>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active schedules due to run at or before <paramref name="nowUtc"/> (for the runner job), oldest-due first.</summary>
    Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    void Add(ReportSchedule schedule);
}
