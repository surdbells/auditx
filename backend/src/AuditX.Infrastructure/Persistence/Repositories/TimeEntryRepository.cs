using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.TimeTracking;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class TimeEntryRepository(AppDbContext db) : ITimeEntryRepository
{
    public Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.TimeEntries.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TimeEntry>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.TimeEntries.AsNoTracking()
            .Where(t => t.AuditId == auditId)
            .OrderByDescending(t => t.WorkDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public void Add(TimeEntry entry) => db.TimeEntries.Add(entry);
}
