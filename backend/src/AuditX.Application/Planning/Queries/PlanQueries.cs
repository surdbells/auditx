using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Planning.Dtos;
using AuditX.Application.Planning.Mapping;
using AuditX.Domain.Enums;

namespace AuditX.Application.Planning.Queries;

public sealed record ListPlansQuery(string? Status, string? Cursor, int? Limit) : IQuery<CursorPage<PlanListItemDto>>;

public sealed class ListPlansQueryHandler(IAnnualPlanRepository plans)
    : IQueryHandler<ListPlansQuery, CursorPage<PlanListItemDto>>
{
    public async Task<CursorPage<PlanListItemDto>> Handle(ListPlansQuery query, CancellationToken cancellationToken)
    {
        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await plans.SearchAsync(query.Status, page, cancellationToken);
        return new CursorPage<PlanListItemDto>(result.Items.Select(p => p.ToListItemDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

public sealed record GetPlanQuery(Guid Id) : IQuery<PlanDto>;

public sealed class GetPlanQueryHandler(IAnnualPlanRepository plans)
    : IQueryHandler<GetPlanQuery, PlanDto>
{
    public async Task<PlanDto> Handle(GetPlanQuery query, CancellationToken cancellationToken)
    {
        var plan = await plans.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Plan", query.Id);
        return plan.ToDto();
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

public sealed class PlanExecutionQueryHandler(IAnnualPlanRepository plans, IClock clock)
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

        return new PlanExecutionDto(total, countsByStatus, percent, behind);
    }
}
