using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Analytics.Dtos;
using AuditX.Application.Analytics.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;

namespace AuditX.Application.Analytics.Queries;

// ---- List recurrence clusters (cursor-paged; ViewAnalytics) ----

public sealed record GetRecurrenceClustersQuery(int? Page, int? PageSize) : IQuery<PagedResult<RecurrenceClusterDto>>;

public sealed class GetRecurrenceClustersQueryHandler(IRecurrenceClusterRepository clusters)
    : IQueryHandler<GetRecurrenceClustersQuery, PagedResult<RecurrenceClusterDto>>
{
    public async Task<PagedResult<RecurrenceClusterDto>> Handle(GetRecurrenceClustersQuery query, CancellationToken cancellationToken)
    {
        var result = await clusters.ListPagedAsync(PageSpec.Of(query.Page, query.PageSize), cancellationToken);
        return result.Map(c => c.ToDto());
    }
}

// ---- Detail drilldown → member exceptions (ViewAnalytics) ----

public sealed record GetRecurrenceClusterDetailQuery(Guid Id) : IQuery<RecurrenceClusterDetailDto>;

public sealed class GetRecurrenceClusterDetailQueryHandler(IRecurrenceClusterRepository clusters)
    : IQueryHandler<GetRecurrenceClusterDetailQuery, RecurrenceClusterDetailDto>
{
    public async Task<RecurrenceClusterDetailDto> Handle(GetRecurrenceClusterDetailQuery query, CancellationToken cancellationToken)
    {
        var cluster = await clusters.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException("Recurrence cluster", query.Id);
        var members = await clusters.GetMembersAsync(cluster.MemberExceptionIds, cancellationToken);
        return cluster.ToDetailDto(members);
    }
}
