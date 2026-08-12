using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Planning.Dtos;
using AuditX.Application.Planning.Mapping;
using AuditX.Domain.Enums;

namespace AuditX.Application.Planning.Queries;

public sealed record ListPlansQuery(string? Status, int? Page, int? PageSize) : IQuery<PagedResult<PlanListItemDto>>;

public sealed class ListPlansQueryHandler(IAnnualPlanRepository plans)
    : IQueryHandler<ListPlansQuery, PagedResult<PlanListItemDto>>
{
    public async Task<PagedResult<PlanListItemDto>> Handle(ListPlansQuery query, CancellationToken cancellationToken)
    {
        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await plans.SearchAsync(query.Status, page, cancellationToken);
        return result.Map(p => p.ToListItemDto());
    }
}

public sealed record GetPlanQuery(Guid Id) : IQuery<PlanDto>;

public sealed class GetPlanQueryHandler(IAnnualPlanRepository plans, IBankSettingsRepository settings)
    : IQueryHandler<GetPlanQuery, PlanDto>
{
    public async Task<PlanDto> Handle(GetPlanQuery query, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Plan", query.Id);
        var bank = await settings.GetAsync(cancellationToken);
        var canLaunch = PlanLaunchPolicy.CanLaunchAudits(plan.Status, bank.AllowAuditLaunchBeforeApproval);
        var canApplyMinorRevision = PlanRevisionPolicy.CanApplyMinorRevision(plan.Status, bank.AllowMinorPlanRevisionAfterApproval);
        return plan.ToDto(canLaunch, canApplyMinorRevision);
    }
}

/// <summary>
/// Resolves a plan item to its owning plan (id + period + item status). Backs the audit-detail "linked plan item"
/// deep link, closing the plan ↔ audit loop from the audit side.
/// </summary>
public sealed record GetPlanItemLocatorQuery(Guid PlanItemId) : IQuery<PlanItemLocatorDto>;

public sealed class GetPlanItemLocatorQueryHandler(IAnnualPlanRepository plans)
    : IQueryHandler<GetPlanItemLocatorQuery, PlanItemLocatorDto>
{
    public async Task<PlanItemLocatorDto> Handle(GetPlanItemLocatorQuery query, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByPlanItemIdAsync(query.PlanItemId, cancellationToken)
            ?? throw new NotFoundException("Plan item", query.PlanItemId);
        var item = plan.Items.First(i => i.Id == query.PlanItemId);
        return new PlanItemLocatorDto(item.Id, plan.Id, plan.PeriodLabel, Common.Enums.EnumExtensions.ToSnake(item.Status));
    }
}

/// <summary>Plan execution progress (US-M3-020): completion percent and behind-schedule items.</summary>
public sealed record PlanExecutionQuery(Guid Id) : IQuery<PlanExecutionDto>;

public sealed class PlanExecutionQueryHandler(IAnnualPlanRepository plans, IAuditRepository audits, IClock clock)
    : IQueryHandler<PlanExecutionQuery, PlanExecutionDto>
{
    public async Task<PlanExecutionDto> Handle(PlanExecutionQuery query, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Plan", query.Id);
        var items = plan.Items;
        var total = items.Count;

        var countsByStatus = items
            .GroupBy(i => Common.Enums.EnumExtensions.ToSnake(i.Status))
            .ToDictionary(g => g.Key, g => g.Count());

        var completed = items.Count(i => i.Status == PlanItemStatus.Completed);
        var percent = total == 0 ? 0m : Math.Round((decimal)completed / total * 100m, 1, MidpointRounding.AwayFromZero);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var behind = items
            .Where(i => i.PlannedEndDate < today && i.Status != PlanItemStatus.Completed)
            .Select(i => i.ToDto())
            .ToArray();

        // Real progress: aggregate checklist-item completion across every audit the plan's items have launched
        // (one per entity per item, since a single item can now cover several entities).
        var progressRows = await audits.GetChecklistProgressByPlanItemIdsAsync(items.Select(i => i.Id).ToArray(), cancellationToken);
        var byPlanItemEntity = progressRows.ToDictionary(r => (r.PlanItemId, r.EntityId));

        var itemProgress = items.SelectMany(i => i.EntityLinks.Select(link =>
        {
            byPlanItemEntity.TryGetValue((i.Id, link.EntityId), out var row);
            var itemTotal = row?.TotalChecklistItems ?? 0;
            var itemResponded = row?.RespondedChecklistItems ?? 0;
            var itemPct = itemTotal == 0 ? 0m : Math.Round((decimal)itemResponded / itemTotal * 100m, 1, MidpointRounding.AwayFromZero);
            return new PlanItemProgressDto(
                i.Id, link.EntityId, link.LinkedAuditId,
                row is null ? null : Common.Enums.EnumExtensions.ToSnake(row.AuditStatus),
                itemTotal, itemResponded, itemPct);
        })).ToArray();

        var totalChecklist = progressRows.Sum(r => r.TotalChecklistItems);
        var respondedChecklist = progressRows.Sum(r => r.RespondedChecklistItems);
        var checklistPercent = totalChecklist == 0
            ? 0m
            : Math.Round((decimal)respondedChecklist / totalChecklist * 100m, 1, MidpointRounding.AwayFromZero);

        return new PlanExecutionDto(
            total, countsByStatus, percent, behind,
            totalChecklist, respondedChecklist, checklistPercent, itemProgress);
    }
}
