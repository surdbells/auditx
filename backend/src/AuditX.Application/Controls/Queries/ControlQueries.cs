using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Controls.Dtos;
using AuditX.Application.Controls.Mapping;

namespace AuditX.Application.Controls.Queries;

public sealed record ListControlsQuery(
    string? Type, string? Effectiveness, Guid? Owner, bool IncludeRetired, string? Search, string? Cursor, int? Limit)
    : IQuery<CursorPage<ControlListItemDto>>;

public sealed class ListControlsQueryHandler(IControlRepository controls)
    : IQueryHandler<ListControlsQuery, CursorPage<ControlListItemDto>>
{
    public async Task<CursorPage<ControlListItemDto>> Handle(ListControlsQuery query, CancellationToken cancellationToken)
    {
        var filter = new ControlSearchFilter(
            ControlParsing.ParseTypeFilter(query.Type),
            ControlParsing.ParseEffectivenessFilter(query.Effectiveness),
            query.Owner,
            query.IncludeRetired,
            query.Search);

        var page = await controls.SearchAsync(filter, PageRequest.Of(query.Cursor, query.Limit), cancellationToken);
        return new CursorPage<ControlListItemDto>(page.Items.Select(c => c.ToListItemDto()).ToArray(), page.NextCursor, page.HasMore);
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
