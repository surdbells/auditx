using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Scheduling.Dtos;
using AuditX.Application.Scheduling.Mapping;

namespace AuditX.Application.Scheduling.Queries;

// ---- List all live schedules (admin console) ----

public sealed record ListReportSchedulesQuery : IQuery<IReadOnlyList<ReportScheduleDto>>;

public sealed class ListReportSchedulesQueryHandler(IReportScheduleRepository schedules)
    : IQueryHandler<ListReportSchedulesQuery, IReadOnlyList<ReportScheduleDto>>
{
    public async Task<IReadOnlyList<ReportScheduleDto>> Handle(ListReportSchedulesQuery query, CancellationToken cancellationToken)
    {
        var rows = await schedules.ListAllAsync(cancellationToken);
        return rows.Select(s => s.ToDto()).ToArray();
    }
}

// ---- Get one ----

public sealed record GetReportScheduleQuery(Guid Id) : IQuery<ReportScheduleDto>;

public sealed class GetReportScheduleQueryHandler(IReportScheduleRepository schedules)
    : IQueryHandler<GetReportScheduleQuery, ReportScheduleDto>
{
    public async Task<ReportScheduleDto> Handle(GetReportScheduleQuery query, CancellationToken cancellationToken)
    {
        var schedule = await schedules.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Report schedule", query.Id);
        return schedule.ToDto();
    }
}
