using AuditX.Application.Abstractions.Authorization;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Authorization;

/// <summary>A single indexed EXISTS — mirrors <c>RespondAuthorization.IsAuditee</c> without loading the aggregate.</summary>
public sealed class AuditIndependenceReader(AppDbContext db) : IAuditIndependenceReader
{
    public Task<bool> IsAuditeeAsync(Guid auditId, Guid userId, CancellationToken cancellationToken = default)
        => db.Audits.AsNoTracking().AnyAsync(
            a => a.Id == auditId && !a.IsSelfAssessment
                && (a.AuditeeUserId == userId
                    || a.TeamMembers.Any(m => m.RemovedAt == null && m.UserId == userId && m.TeamRole == TeamRole.Auditee)),
            cancellationToken);
}
