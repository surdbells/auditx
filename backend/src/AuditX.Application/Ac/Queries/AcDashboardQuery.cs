using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Ac.Queries;

/// <summary>
/// The read-only Audit-Committee dashboard (M13; ACMember). LIVE aggregates from the M9
/// <see cref="IAnalyticsQueryService"/> port — AGGREGATES ONLY. The sanctions section is sourced exclusively from
/// <see cref="IAnalyticsQueryService.SanctionsConsistencyAsync"/>, whose DTO physically omits the subject id (no
/// raw sanctions cases are ever joined). Material-finding detail is hidden per-requester for restricted findings
/// (FR-M13-009) while the aggregate severity/portfolio counts stay coherent.
/// </summary>
public sealed record AcDashboardQuery : IQuery<AcDashboardDto>;

public sealed class AcDashboardQueryHandler(
    IAnalyticsQueryService analytics, IRecurrenceClusterRepository clusters,
    IFindingVisibilityRestrictionRepository restrictions, ICurrentUser currentUser)
    : IQueryHandler<AcDashboardQuery, AcDashboardDto>
{
    public async Task<AcDashboardDto> Handle(AcDashboardQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var planStatus = await analytics.PlanStatusAsync(cancellationToken);
        var portfolio = await analytics.ExceptionPortfolioAsync(cancellationToken);
        var materialFindings = await analytics.MaterialFindingsAsync(cancellationToken);
        var sanctions = await analytics.SanctionsConsistencyAsync(cancellationToken);
        var recurrencePage = await clusters.ListPagedAsync(new Common.Models.PageSpec(1, 50), cancellationToken);

        var allowLists = await AcVisibility.LoadExceptionAllowListsAsync(restrictions, cancellationToken);

        var findings = materialFindings.Select(f =>
        {
            var restricted = allowLists.TryGetValue(f.ExceptionId, out var allowed) && !allowed.Contains(userId);
            return new AcMaterialFindingDto(
                f.ExceptionId, f.AuditId, restricted ? null : f.Title, f.Severity, f.Status,
                f.AuditableEntityId, f.RaisedAt, f.TargetDate, restricted);
        }).ToArray();

        return new AcDashboardDto(
            planStatus.TotalPlans,
            planStatus.TotalItems,
            planStatus.Completed,
            planStatus.CompletionPercent,
            portfolio.TotalOpen,
            portfolio.AverageClosureDays,
            portfolio.BySeverity.Select(s => new AcSeverityCountDto(s.Severity, s.Count)).ToArray(),
            findings,
            sanctions.TotalCases,
            sanctions.OverallGridAdherencePercent,
            sanctions.OverallAppealRatePercent,
            sanctions.ByBusinessUnit.Select(r => new AcSanctionsConsistencyRowDto(
                r.BusinessUnit, r.CaseCount, r.WithinGridCount, r.GridAdherencePercent, r.DeviationCount, r.AppealCount, r.AppealRatePercent)).ToArray(),
            recurrencePage.Items.Select(c => new AcRecurrenceClusterDto(
                c.Id, c.AuditableEntityId, c.Category, c.ClosedExceptionCount, c.WindowMonths, c.FirstOccurredAt, c.LastOccurredAt)).ToArray());
    }
}
