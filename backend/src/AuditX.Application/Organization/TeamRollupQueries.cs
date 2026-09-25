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

/// <summary>One row of the management-line analytics dimension: a manager and their whole team's finding load.</summary>
public sealed record ManagementLineScorecardDto(
    Guid ManagerId, string ManagerName, int DirectReports, int TotalReports, int OpenFindings, int OverdueFindings);

/// <summary>Management-line scorecards: every manager with their team's open/overdue finding totals (analytics dimension).</summary>
public sealed record ManagementLineScorecardsQuery : IQuery<IReadOnlyList<ManagementLineScorecardDto>>;

public sealed class ManagementLineScorecardsQueryHandler(
    IUserRepository users,
    IReportingLineResolver reportingLine,
    IExceptionRepository exceptions,
    IClock clock)
    : IQueryHandler<ManagementLineScorecardsQuery, IReadOnlyList<ManagementLineScorecardDto>>
{
    public async Task<IReadOnlyList<ManagementLineScorecardDto>> Handle(ManagementLineScorecardsQuery query, CancellationToken cancellationToken)
    {
        var managerTeams = await reportingLine.GetManagerSubtreesAsync(cancellationToken);
        if (managerTeams.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var allMemberIds = managerTeams.SelectMany(m => m.SubtreeMemberIds).Distinct().ToArray();
        var counts = await exceptions.CountOpenAndOverdueByOwnersAsync(allMemberIds, today, cancellationToken);

        var managers = await users.GetByIdsAsync(managerTeams.Select(m => m.ManagerId).ToArray(), cancellationToken);
        var nameById = managers.ToDictionary(u => u.Id, u => u.DisplayName);

        return managerTeams
            .Select(team =>
            {
                var open = 0;
                var overdue = 0;
                foreach (var memberId in team.SubtreeMemberIds)
                {
                    if (counts.TryGetValue(memberId, out var c))
                    {
                        open += c.Open;
                        overdue += c.Overdue;
                    }
                }

                return new ManagementLineScorecardDto(
                    team.ManagerId, nameById.TryGetValue(team.ManagerId, out var n) ? n : "(unknown)",
                    team.DirectReportCount, team.SubtreeMemberIds.Count, open, overdue);
            })
            .OrderByDescending(s => s.OverdueFindings)
            .ThenByDescending(s => s.OpenFindings)
            .ThenBy(s => s.ManagerName)
            .ToArray();
    }
}
