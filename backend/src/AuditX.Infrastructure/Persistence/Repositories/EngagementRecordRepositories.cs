using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Engagement;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence.Repositories;

public sealed class EngagementLetterRepository(AppDbContext db) : IEngagementLetterRepository
{
    public Task<EngagementLetter?> GetByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => db.EngagementLetters.Include(l => l.PreviousRevisions).FirstOrDefaultAsync(l => l.AuditId == auditId, cancellationToken);

    public Task<EngagementLetter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.EngagementLetters.Include(l => l.PreviousRevisions).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public void Add(EngagementLetter letter) => db.EngagementLetters.Add(letter);
}

public sealed class AuditMeetingRepository(AppDbContext db) : IAuditMeetingRepository
{
    public async Task<IReadOnlyList<AuditMeeting>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default)
        => await db.AuditMeetings.Where(m => m.AuditId == auditId).OrderBy(m => m.Kind).ToListAsync(cancellationToken);

    public void Add(AuditMeeting meeting) => db.AuditMeetings.Add(meeting);
}
