using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.Audits;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Execution.Queries;

public sealed record GetChecklistResponseQuery(Guid AuditId, Guid ItemId) : IQuery<ChecklistResponseDto?>;

public sealed class GetChecklistResponseQueryHandler(IAuditRepository audits, ICurrentUser currentUser)
    : IQueryHandler<GetChecklistResponseQuery, ChecklistResponseDto?>
{
    public async Task<ChecklistResponseDto?> Handle(GetChecklistResponseQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        var response = entity.Responses.FirstOrDefault(r => r.ChecklistItemId == query.ItemId);
        if (response is null)
        {
            return null;
        }

        // A draft is visible only to its author (US-M5-003).
        if (response.IsDraft && response.ResponderUserId != currentUser.UserId)
        {
            return null;
        }

        return response.ToDto();
    }
}

public sealed record GetResponseHistoryQuery(Guid AuditId, Guid ItemId) : IQuery<IReadOnlyList<ResponseHistoryEntryDto>>;

public sealed class GetResponseHistoryQueryHandler(IAuditRepository audits, IAuditTrailReader trail, ICurrentUser currentUser)
    : IQueryHandler<GetResponseHistoryQuery, IReadOnlyList<ResponseHistoryEntryDto>>
{
    public async Task<IReadOnlyList<ResponseHistoryEntryDto>> Handle(GetResponseHistoryQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        var response = entity.Responses.FirstOrDefault(r => r.ChecklistItemId == query.ItemId);
        if (response is null)
        {
            return [];
        }

        // A draft's trail (after-state carries the draft comment) is visible only to its author (US-M5-003).
        if (response.IsDraft && response.ResponderUserId != currentUser.UserId)
        {
            return [];
        }

        var entries = await trail.GetForTargetAsync(AuditTargetTypes.ChecklistResponse, response.Id, eventType: null, limit: 200, cancellationToken);
        return entries
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new ResponseHistoryEntryDto(e.Id, e.EventType, e.ActorUserId, e.OccurredAtUtc, e.AfterStateJson, e.BeforeStateJson))
            .ToArray();
    }
}

public sealed record GetChecklistProgressQuery(Guid AuditId) : IQuery<ChecklistProgressDto>;

public sealed class GetChecklistProgressQueryHandler(IAuditRepository audits)
    : IQueryHandler<GetChecklistProgressQuery, ChecklistProgressDto>
{
    public async Task<ChecklistProgressDto> Handle(GetChecklistProgressQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);

        var items = entity.ChecklistItems
            .OrderBy(i => i.OrderIndex)
            .Select(i =>
            {
                var finalised = entity.Responses.FirstOrDefault(r => r.ChecklistItemId == i.Id && !r.IsDraft);
                return new ChecklistProgressItemDto(
                    i.Id, i.SectionName, i.OrderIndex, i.Prompt, i.ItemState.ToSnake(),
                    finalised?.Verdict is { } v ? v.ToSnake() : null, i.IsRequired, i.AssignedUserId, i.HasException,
                    i.ResponseType.ToSnake(), i.ResponseConfigJson, finalised?.ValueJson, finalised?.Score, i.RiskRating?.ToSnake());
            })
            .ToArray();

        return new ChecklistProgressDto(
            items.Length,
            items.Count(i => i.ItemState == ChecklistItemState.Responded.ToSnake()),
            items.Count(i => i.ItemState == ChecklistItemState.InProgress.ToSnake()),
            items.Count(i => i.ItemState == ChecklistItemState.NotStarted.ToSnake()),
            items);
    }
}

public sealed record ListFailWithoutExceptionQuery(Guid AuditId) : IQuery<FailWithoutExceptionDto>;

public sealed class ListFailWithoutExceptionQueryHandler(IAuditRepository audits)
    : IQueryHandler<ListFailWithoutExceptionQuery, FailWithoutExceptionDto>
{
    public async Task<FailWithoutExceptionDto> Handle(ListFailWithoutExceptionQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);

        var items = entity.ChecklistItems
            .Where(i => !i.HasException && i.FailJustification is null
                && entity.Responses.Any(r => r.ChecklistItemId == i.Id && !r.IsDraft && r.Verdict == ResponseVerdict.Fail))
            .Select(i => new FailWithoutExceptionItemDto(
                i.Id, i.Prompt,
                entity.Responses.First(r => r.ChecklistItemId == i.Id && !r.IsDraft).Comment, i.AssignedUserId))
            .ToArray();

        return new FailWithoutExceptionDto(items.Length, items);
    }
}

public sealed record GetReviewSummaryQuery(Guid AuditId) : IQuery<ReviewSummaryDto>;

public sealed class GetReviewSummaryQueryHandler(IAuditRepository audits)
    : IQueryHandler<GetReviewSummaryQuery, ReviewSummaryDto>
{
    public async Task<ReviewSummaryDto> Handle(GetReviewSummaryQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        var finalised = entity.Responses.Where(r => !r.IsDraft).ToArray();
        return new ReviewSummaryDto(
            entity.ChecklistItems.Count,
            entity.ChecklistItems.Count(i => i.ItemState == ChecklistItemState.Responded),
            finalised.Count(r => r.Verdict == ResponseVerdict.Pass),
            finalised.Count(r => r.Verdict == ResponseVerdict.Fail),
            finalised.Count(r => r.Verdict == ResponseVerdict.Na),
            entity.ChecklistItems.Count(i => i.HasException));
    }
}
