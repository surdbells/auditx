using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;

namespace AuditX.Application.Audits.Queries;

public sealed record ListAuditsQuery(string? Status, string? AuditType, Guid? Lead, Guid? PlanItem, string? Cursor, int? Limit)
    : IQuery<CursorPage<AuditListItemDto>>;

public sealed class ListAuditsQueryHandler(IAuditRepository audits)
    : IQueryHandler<ListAuditsQuery, CursorPage<AuditListItemDto>>
{
    public async Task<CursorPage<AuditListItemDto>> Handle(ListAuditsQuery query, CancellationToken cancellationToken)
    {
        AuditStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = Enum.TryParse<AuditStatus>(query.Status.Replace("_", string.Empty), ignoreCase: true, out var parsed)
                ? parsed
                : throw new ConflictException("invalid_status", $"Unknown audit status '{query.Status}'.");
        }

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await audits.SearchAsync(status, query.AuditType, query.Lead, query.PlanItem, page, cancellationToken);
        return new CursorPage<AuditListItemDto>(result.Items.Select(a => a.ToListItemDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

public sealed record GetAuditQuery(Guid Id) : IQuery<AuditDto>;

public sealed class GetAuditQueryHandler(IAuditRepository audits)
    : IQueryHandler<GetAuditQuery, AuditDto>
{
    public async Task<AuditDto> Handle(GetAuditQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Audit", query.Id);
        return entity.ToDto();
    }
}

public sealed record AuditCountsQuery : IQuery<AuditCountsDto>;

public sealed class AuditCountsQueryHandler(IAuditRepository audits)
    : IQueryHandler<AuditCountsQuery, AuditCountsDto>
{
    public async Task<AuditCountsDto> Handle(AuditCountsQuery query, CancellationToken cancellationToken)
        => new(await audits.CountsByStatusAsync(cancellationToken));
}
