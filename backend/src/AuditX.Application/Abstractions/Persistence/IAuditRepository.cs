using AuditX.Application.Common.Models;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Checklist-completion rollup for the audit linked to one entity of a plan item (feeds plan-progress aggregation).</summary>
public sealed record PlanItemAuditProgress(Guid PlanItemId, Guid EntityId, Guid AuditId, AuditStatus AuditStatus, int TotalChecklistItems, int RespondedChecklistItems);

public interface IAuditRepository
{
    Task<Audit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Audit>> SearchAsync(
        AuditStatus? status, string? auditType, Guid? leadUserId, Guid? planItemId, string? search, PageSpec page, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountsByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Planned audits whose start date has arrived, for the auto-start job (US-M4-011).</summary>
    Task<IReadOnlyList<Audit>> GetPlannedDueToStartAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-plan-item checklist-completion rollup across the audits launched from the given plan items.
    /// Used to aggregate real checklist progress into an annual plan's execution view (US-M3-020).
    /// </summary>
    Task<IReadOnlyList<PlanItemAuditProgress>> GetChecklistProgressByPlanItemIdsAsync(
        IReadOnlyCollection<Guid> planItemIds, CancellationToken cancellationToken = default);

    void Add(Audit audit);
}
