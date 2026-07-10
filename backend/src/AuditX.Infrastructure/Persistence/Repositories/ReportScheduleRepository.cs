using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class ReportScheduleRepository(AppDbContext db) : IReportScheduleRepository
{
    public Task<ReportSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ReportSchedule>> ListAllAsync(CancellationToken cancellationToken = default)
        => await db.ReportSchedules.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        => await db.ReportSchedules
            .Where(s => s.IsActive && s.NextRunAt <= nowUtc)
            .OrderBy(s => s.NextRunAt)
            .ToListAsync(cancellationToken);

    public void Add(ReportSchedule schedule) => db.ReportSchedules.Add(schedule);
}
