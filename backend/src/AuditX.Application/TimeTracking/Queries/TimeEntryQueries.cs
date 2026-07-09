using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.TimeTracking.Dtos;
using AuditX.Application.TimeTracking.Mapping;
using AuditX.Domain.TimeTracking;

namespace AuditX.Application.TimeTracking.Queries;

// ---- List an audit's entries ----

public sealed record ListAuditTimeEntriesQuery(Guid AuditId) : IQuery<IReadOnlyList<TimeEntryDto>>;

public sealed class ListAuditTimeEntriesQueryHandler(
    IAuditRepository audits, ITimeEntryRepository entries, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<ListAuditTimeEntriesQuery, IReadOnlyList<TimeEntryDto>>
{
    public async Task<IReadOnlyList<TimeEntryDto>> Handle(ListAuditTimeEntriesQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", query.AuditId);
        await TimeEntryAccess.EnsureCanViewAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var rows = await entries.ListByAuditAsync(query.AuditId, cancellationToken);
        return rows.Select(e => e.ToDto()).ToArray();
    }
}

// ---- Budget-vs-actual summary for an audit ----

public sealed record GetAuditTimeSummaryQuery(Guid AuditId) : IQuery<TimeEntrySummaryDto>;

public sealed class GetAuditTimeSummaryQueryHandler(
    IAuditRepository audits, ITimeEntryRepository entries, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<GetAuditTimeSummaryQuery, TimeEntrySummaryDto>
{
    public async Task<TimeEntrySummaryDto> Handle(GetAuditTimeSummaryQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", query.AuditId);
        await TimeEntryAccess.EnsureCanViewAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var rows = await entries.ListByAuditAsync(query.AuditId, cancellationToken);
        var actual = rows.Sum(e => e.Hours);
        var budget = auditEntity.BudgetedHours;

        var byCategory = rows
            .GroupBy(e => e.Category)
            .Select(g => new CategoryHoursDto(g.Key.ToSnake(), g.Sum(e => e.Hours)))
            .OrderByDescending(c => c.Hours)
            .ToArray();

        var byUser = rows
            .GroupBy(e => e.UserId)
            .Select(g => new UserHoursDto(g.Key, g.Sum(e => e.Hours)))
            .OrderByDescending(u => u.Hours)
            .ToArray();

        return new TimeEntrySummaryDto(
            query.AuditId,
            budget,
            actual,
            budget is { } b ? b - actual : null,
            budget is { } bd && bd > 0 ? (double)(actual / bd) * 100d : null,
            rows.Count,
            byUser.Length,
            byCategory,
            byUser);
    }
}
