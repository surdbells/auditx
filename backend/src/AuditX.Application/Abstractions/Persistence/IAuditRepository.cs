using AuditX.Application.Common.Models;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Persistence;

public interface IAuditRepository
{
    Task<Audit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CursorPage<Audit>> SearchAsync(
        AuditStatus? status, string? auditType, Guid? leadUserId, Guid? planItemId, string? search, PageRequest page, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountsByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Planned audits whose start date has arrived, for the auto-start job (US-M4-011).</summary>
    Task<IReadOnlyList<Audit>> GetPlannedDueToStartAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);

    void Add(Audit audit);
}
