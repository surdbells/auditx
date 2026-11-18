using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Engagement;
using AuditX.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class EngagementLetterRepository(AppDbContext db) : IEngagementLetterRepository
{
    public Task<EngagementLetter?> GetByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => db.EngagementLetters.Include(l => l.PreviousRevisions).FirstOrDefaultAsync(l => l.AuditId == auditId, cancellationToken);

    public Task<EngagementLetter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.EngagementLetters.Include(l => l.PreviousRevisions).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, EngagementLetterStatus>> GetStatusesByAuditAsync(
        IReadOnlyCollection<Guid> auditIds, CancellationToken cancellationToken = default)
    {
        if (auditIds.Count == 0)
        {
            return new Dictionary<Guid, EngagementLetterStatus>();
        }

        var ids = auditIds.ToArray();
        return await db.EngagementLetters.AsNoTracking()
            .Where(l => ids.Contains(l.AuditId))
            .Select(l => new { l.AuditId, l.Status })
            .ToDictionaryAsync(l => l.AuditId, l => l.Status, cancellationToken);
    }

    public void Add(EngagementLetter letter) => db.EngagementLetters.Add(letter);
}

public sealed class AuditMeetingRepository(AppDbContext db) : IAuditMeetingRepository
{
    public async Task<IReadOnlyList<AuditMeeting>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.AuditMeetings.Where(m => m.AuditId == auditId).OrderBy(m => m.Kind).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecordedMeeting>> ListRecordedAsync(
        IReadOnlyCollection<Guid> auditIds, CancellationToken cancellationToken = default)
    {
        if (auditIds.Count == 0)
        {
            return [];
        }

        var ids = auditIds.ToArray();
        return await db.AuditMeetings.AsNoTracking()
            .Where(m => ids.Contains(m.AuditId))
            .Select(m => new RecordedMeeting(m.AuditId, m.Kind))
            .ToListAsync(cancellationToken);
    }

    public void Add(AuditMeeting meeting) => db.AuditMeetings.Add(meeting);
}
