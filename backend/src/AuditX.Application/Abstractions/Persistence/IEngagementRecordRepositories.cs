using AuditX.Domain.Engagement;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Persistence port for an audit's <see cref="EngagementLetter"/> (one per audit).</summary>
public interface IEngagementLetterRepository
{
    /// <summary>The audit's letter, tracked for change, with its previous revisions; null when none has been written.</summary>
    Task<EngagementLetter?> GetByAuditAsync(Guid auditId, CancellationToken cancellationToken = default);

    /// <summary>A letter by its id, tracked for change, with its previous revisions.</summary>
    Task<EngagementLetter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(EngagementLetter letter);
}

/// <summary>Persistence port for an audit's kickoff and exit <see cref="AuditMeeting"/> records.</summary>
public interface IAuditMeetingRepository
{
    /// <summary>The audit's meeting records, tracked for change (at most one of each kind).</summary>
    Task<IReadOnlyList<AuditMeeting>> ListByAuditAsync(Guid auditId, CancellationToken cancellationToken = default);

    void Add(AuditMeeting meeting);
}
