using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Controls.Dtos;
using AuditX.Application.Controls.Mapping;

namespace AuditX.Application.Controls.Queries;

public sealed record ListControlsQuery(
    string? Type, string? Effectiveness, Guid? Owner, bool IncludeRetired, string? Search, int? Page, int? PageSize)
    : IQuery<PagedResult<ControlListItemDto>>;

public sealed class ListControlsQueryHandler(IControlRepository controls)
    : IQueryHandler<ListControlsQuery, PagedResult<ControlListItemDto>>
{
    public async Task<PagedResult<ControlListItemDto>> Handle(ListControlsQuery query, CancellationToken cancellationToken)
    {
        var filter = new ControlSearchFilter(
            ControlParsing.ParseTypeFilter(query.Type),
            ControlParsing.ParseEffectivenessFilter(query.Effectiveness),
            query.Owner,
            query.IncludeRetired,
            query.Search);

        var result = await controls.SearchAsync(filter, PageSpec.Of(query.Page, query.PageSize), cancellationToken);
        return result.Map(c => c.ToListItemDto());
    }
}

public sealed record GetControlQuery(Guid Id) : IQuery<ControlDto>;

public sealed class GetControlQueryHandler(IControlRepository controls)
    : IQueryHandler<GetControlQuery, ControlDto>
{
    public async Task<ControlDto> Handle(GetControlQuery query, CancellationToken cancellationToken)
    {
        var control = await controls.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Control", query.Id);
        return control.ToDto();
    }
}
