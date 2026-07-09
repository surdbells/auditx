using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Risks.Dtos;
using AuditX.Application.Risks.Mapping;

namespace AuditX.Application.Risks.Queries;

public sealed record ListRisksQuery(
    string? Status, string? Category, Guid? Owner, string? Band, bool IncludeClosed, string? Search, string? Cursor, int? Limit)
    : IQuery<CursorPage<RiskListItemDto>>;

public sealed class ListRisksQueryHandler(IRiskRepository risks)
    : IQueryHandler<ListRisksQuery, CursorPage<RiskListItemDto>>
{
    public async Task<CursorPage<RiskListItemDto>> Handle(ListRisksQuery query, CancellationToken cancellationToken)
    {
        var filter = new RiskSearchFilter(
            RiskParsing.ParseStatusFilter(query.Status),
            string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(),
            query.Owner,
            RiskParsing.ParseBandFilter(query.Band),
            query.IncludeClosed,
            query.Search);

        var page = await risks.SearchAsync(filter, PageRequest.Of(query.Cursor, query.Limit), cancellationToken);
        return new CursorPage<RiskListItemDto>(page.Items.Select(r => r.ToListItemDto()).ToArray(), page.NextCursor, page.HasMore);
    }
}

public sealed record GetRiskQuery(Guid Id) : IQuery<RiskDto>;

public sealed class GetRiskQueryHandler(IRiskRepository risks)
    : IQueryHandler<GetRiskQuery, RiskDto>
{
    public async Task<RiskDto> Handle(GetRiskQuery query, CancellationToken cancellationToken)
    {
        var risk = await risks.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Risk", query.Id);
        return risk.ToDto();
    }
}
