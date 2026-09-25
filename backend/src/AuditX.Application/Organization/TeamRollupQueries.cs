using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Organization;

/// <summary>One member of a manager's team roll-up: a direct report and their own open/overdue finding counts.</summary>
public sealed record TeamRollupMemberDto(Guid UserId, string DisplayName, int OpenFindings, int OverdueFindings);

/// <summary>
/// A management-line roll-up for a manager: totals across their whole reporting subtree (transitive reports), plus a
/// per-direct-report breakdown. Backs the "my team's findings" view and the management-line analytics dimension.
/// </summary>
public sealed record TeamExceptionRollupDto(
    Guid ManagerId, int ReportCount, int OpenFindings, int OverdueFindings, IReadOnlyList<TeamRollupMemberDto> DirectReports);

public sealed record GetTeamExceptionRollupQuery(Guid ManagerId) : IQuery<TeamExceptionRollupDto>;

public sealed class GetTeamExceptionRollupQueryHandler(
    IUserRepository users,
    IReportingLineResolver reportingLine,
    IExceptionRepository exceptions,
    IClock clock)
    : IQueryHandler<GetTeamExceptionRollupQuery, TeamExceptionRollupDto>
{
    public async Task<TeamExceptionRollupDto> Handle(GetTeamExceptionRollupQuery query, CancellationToken cancellationToken)
    {
        _ = await users.GetByIdAsync(query.ManagerId, cancellationToken)
            ?? throw new NotFoundException("User", query.ManagerId);

        var subtree = await reportingLine.GetReportSubtreeAsync(query.ManagerId, cancellationToken);
        if (subtree.Count == 0)
        {
            return new TeamExceptionRollupDto(query.ManagerId, 0, 0, 0, []);
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var counts = await exceptions.CountOpenAndOverdueByOwnersAsync(subtree, today, cancellationToken);
        var totalOpen = counts.Values.Sum(c => c.Open);
        var totalOverdue = counts.Values.Sum(c => c.Overdue);

        var directReportIds = (await reportingLine.GetDirectReportsAsync(query.ManagerId, cancellationToken)).ToHashSet();
        var directReportUsers = await users.GetByIdsAsync(directReportIds, cancellationToken);
        var directReports = directReportUsers
            .Select(u =>
            {
                counts.TryGetValue(u.Id, out var c);
                return new TeamRollupMemberDto(u.Id, u.DisplayName, c.Open, c.Overdue);
            })
            .OrderByDescending(m => m.OverdueFindings)
            .ThenByDescending(m => m.OpenFindings)
            .ThenBy(m => m.DisplayName)
            .ToArray();

        return new TeamExceptionRollupDto(query.ManagerId, subtree.Count, totalOpen, totalOverdue, directReports);
    }
}
