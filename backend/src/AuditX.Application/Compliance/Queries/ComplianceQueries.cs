using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Compliance.Dtos;
using AuditX.Application.Compliance.Mapping;
using AuditX.Application.Exceptions;

namespace AuditX.Application.Compliance.Queries;

// ---- Regulation register ----

public sealed record ListRegulationsQuery(string? Category, bool IncludeRetired, string? Search, int? Page, int? PageSize)
    : IQuery<PagedResult<RegulationListItemDto>>;

public sealed class ListRegulationsQueryHandler(IRegulationRepository regulations)
    : IQueryHandler<ListRegulationsQuery, PagedResult<RegulationListItemDto>>
{
    public async Task<PagedResult<RegulationListItemDto>> Handle(ListRegulationsQuery query, CancellationToken cancellationToken)
    {
        var filter = new RegulationSearchFilter(
            string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(), query.IncludeRetired, query.Search);
        var result = await regulations.SearchAsync(filter, PageSpec.Of(query.Page, query.PageSize), cancellationToken);
        return result.Map(r => r.ToListItemDto());
    }
}

public sealed record GetRegulationQuery(Guid Id) : IQuery<RegulationDto>;

public sealed class GetRegulationQueryHandler(IRegulationRepository regulations)
    : IQueryHandler<GetRegulationQuery, RegulationDto>
{
    public async Task<RegulationDto> Handle(GetRegulationQuery query, CancellationToken cancellationToken)
    {
        var regulation = await regulations.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Regulation", query.Id);
        return regulation.ToDto();
    }
}

// ---- A finding's linked controls + regulations ----

public sealed record ListFindingLinksQuery(Guid ExceptionId)
    : IQuery<FindingLinksDto>;

public sealed record FindingLinksDto(
    IReadOnlyList<FindingControlLinkDto> Controls, IReadOnlyList<FindingRegulationLinkDto> Regulations);

public sealed class ListFindingLinksQueryHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IFindingLinkRepository links,
    IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListFindingLinksQuery, FindingLinksDto>
{
    public async Task<FindingLinksDto> Handle(ListFindingLinksQuery query, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(query.ExceptionId, cancellationToken) ?? throw new NotFoundException("Exception", query.ExceptionId);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var controls = await links.ListControlLinksAsync(query.ExceptionId, cancellationToken);
        var regulations = await links.ListRegulationLinksAsync(query.ExceptionId, cancellationToken);
        return new FindingLinksDto(
            controls.Select(c => new FindingControlLinkDto(c.LinkId, c.ControlId, c.Code, c.Title, c.LinkedAt)).ToArray(),
            regulations.Select(r => new FindingRegulationLinkDto(r.LinkId, r.RegulationId, r.Code, r.Name, r.LinkedAt)).ToArray());
    }
}
