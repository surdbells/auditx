using AuditX.Application.Common.Models;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Checklist-completion rollup for the audit linked to one entity of a plan item (feeds plan-progress aggregation).</summary>
public sealed record PlanItemAuditProgress(Guid PlanItemId, Guid EntityId, Guid AuditId, AuditStatus AuditStatus, int TotalChecklistItems, int RespondedChecklistItems);

/// <summary>
/// Whose engagements the board lists: the audits <paramref name="UserId"/> is on the active team of (the auditee
/// included), plus the audits they manage. Those are every audit (<paramref name="AllAudits"/>), the audits a grant
/// names (<paramref name="AuditIds"/>), and the audits of entities in <paramref name="OrgUnitIds"/>.
/// </summary>
public sealed record EngagementBoardReach(
    Guid UserId, bool AllAudits, IReadOnlyCollection<Guid> AuditIds, IReadOnlyCollection<Guid> OrgUnitIds)
{
    /// <summary>Whether the user manages the audit, given the org unit of the entity it covers.</summary>
    public bool Manages(Guid auditId, Guid? orgUnitId)
        => AllAudits || AuditIds.Contains(auditId) || (orgUnitId is { } unit && OrgUnitIds.Contains(unit));
}

/// <summary>An audit on the engagement board, with the org unit of the entity it covers (null when it covers none).</summary>
public sealed record EngagementBoardAudit(Audit Audit, Guid? OrgUnitId);

public interface IAuditRepository
{
    Task<Audit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="scope">
    /// Resource-scope guard (not a user filter): when set, only audits the user is on (team or auditee) or whose
    /// entity belongs to one of the listed org units. Null for an organisation-wide caller.
    /// </param>
    Task<PagedResult<Audit>> SearchAsync(
        AuditStatus? status, string? auditType, Guid? leadUserId, Guid? planItemId, string? search, PageSpec page,
        Abstractions.Authorization.ListScope? scope = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The audits on a user's engagement board (see <see cref="EngagementBoardReach"/>), newest first and at most
    /// <paramref name="max"/>, read-only. Each comes with what the journey reads: the team, checklist, responses,
    /// review notes and independence declarations.
    /// </summary>
    Task<IReadOnlyList<EngagementBoardAudit>> ListForEngagementBoardAsync(
        EngagementBoardReach reach, AuditStatus? status, int max, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountsByStatusAsync(
        Abstractions.Authorization.ListScope? scope = null, CancellationToken cancellationToken = default);

    /// <summary>Planned audits whose start date has arrived, for the auto-start job (US-M4-011).</summary>
    Task<IReadOnlyList<Audit>> GetPlannedDueToStartAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-plan-item checklist-completion rollup across the audits launched from the given plan items.
    /// Used to aggregate real checklist progress into an annual plan's execution view (US-M3-020).
    /// </summary>
    Task<IReadOnlyList<PlanItemAuditProgress>> GetChecklistProgressByPlanItemIdsAsync(
        IReadOnlyCollection<Guid> planItemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The latest completion date among the COMPLETED, independent (non-self-assessment) audits of an entity,
    /// optionally ignoring one audit. Null when there is none — the entity has not been audited.
    /// </summary>
    Task<DateOnly?> GetLatestCompletionDateForEntityAsync(Guid auditableEntityId, Guid? excludingAuditId, CancellationToken cancellationToken = default);

    void Add(Audit audit);
}
