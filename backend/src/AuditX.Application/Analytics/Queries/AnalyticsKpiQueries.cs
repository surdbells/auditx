using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Universe;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Analytics.Queries;

// ---- Function performance (ViewAnalytics) ----

public sealed record FunctionPerformanceQuery : IQuery<FunctionPerformanceDto>;

public sealed class FunctionPerformanceQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<FunctionPerformanceQuery, FunctionPerformanceDto>
{
    public Task<FunctionPerformanceDto> Handle(FunctionPerformanceQuery query, CancellationToken cancellationToken)
        => analytics.FunctionPerformanceAsync(cancellationToken);
}

// ---- Exception portfolio (ViewAnalytics) ----

public sealed record ExceptionPortfolioQuery : IQuery<ExceptionPortfolioDto>;

public sealed class ExceptionPortfolioQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<ExceptionPortfolioQuery, ExceptionPortfolioDto>
{
    public Task<ExceptionPortfolioDto> Handle(ExceptionPortfolioQuery query, CancellationToken cancellationToken)
        => analytics.ExceptionPortfolioAsync(cancellationToken);
}

// ---- Sanctions consistency (ViewAnalytics; projection omits subject identity) ----

public sealed record SanctionsConsistencyQuery : IQuery<SanctionsConsistencyDto>;

public sealed class SanctionsConsistencyQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<SanctionsConsistencyQuery, SanctionsConsistencyDto>
{
    public Task<SanctionsConsistencyDto> Handle(SanctionsConsistencyQuery query, CancellationToken cancellationToken)
        => analytics.SanctionsConsistencyAsync(cancellationToken);
}

// ---- Material findings (ViewAnalytics) ----

public sealed record MaterialFindingsQuery : IQuery<IReadOnlyList<MaterialFindingDto>>;

public sealed class MaterialFindingsQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<MaterialFindingsQuery, IReadOnlyList<MaterialFindingDto>>
{
    public Task<IReadOnlyList<MaterialFindingDto>> Handle(MaterialFindingsQuery query, CancellationToken cancellationToken)
        => analytics.MaterialFindingsAsync(cancellationToken);
}

// ---- Plan status (ViewAnalytics) ----

public sealed record PlanStatusQuery : IQuery<PlanStatusDto>;

public sealed class PlanStatusQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<PlanStatusQuery, PlanStatusDto>
{
    public Task<PlanStatusDto> Handle(PlanStatusQuery query, CancellationToken cancellationToken)
        => analytics.PlanStatusAsync(cancellationToken);
}

// ---- Department / business-unit scorecards (ViewAnalytics) ----

public sealed record OrgUnitScorecardsQuery : IQuery<IReadOnlyList<OrgUnitScorecardDto>>;

public sealed class OrgUnitScorecardsQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<OrgUnitScorecardsQuery, IReadOnlyList<OrgUnitScorecardDto>>
{
    public Task<IReadOnlyList<OrgUnitScorecardDto>> Handle(OrgUnitScorecardsQuery query, CancellationToken cancellationToken)
        => analytics.OrgUnitScorecardsAsync(cancellationToken);
}

// ---- Time & effort: budget-vs-actual + utilisation (ViewAnalytics) ----

public sealed record BudgetVsActualQuery : IQuery<IReadOnlyList<BudgetVsActualDto>>;

public sealed class BudgetVsActualQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<BudgetVsActualQuery, IReadOnlyList<BudgetVsActualDto>>
{
    public Task<IReadOnlyList<BudgetVsActualDto>> Handle(BudgetVsActualQuery query, CancellationToken cancellationToken)
        => analytics.BudgetVsActualAsync(cancellationToken);
}

public sealed record UtilisationByUserQuery : IQuery<IReadOnlyList<AuditorUtilisationDto>>;

public sealed class UtilisationByUserQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<UtilisationByUserQuery, IReadOnlyList<AuditorUtilisationDto>>
{
    public Task<IReadOnlyList<AuditorUtilisationDto>> Handle(UtilisationByUserQuery query, CancellationToken cancellationToken)
        => analytics.UtilisationByUserAsync(cancellationToken);
}

// ---- Risk analytics (ViewAnalytics) ----

public sealed record RiskHeatmapQuery : IQuery<RiskHeatmapDto>;

public sealed class RiskHeatmapQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<RiskHeatmapQuery, RiskHeatmapDto>
{
    public Task<RiskHeatmapDto> Handle(RiskHeatmapQuery query, CancellationToken cancellationToken)
        => analytics.RiskHeatmapAsync(cancellationToken);
}

public sealed record RiskRegisterSummaryQuery : IQuery<RiskRegisterSummaryDto>;

public sealed class RiskRegisterSummaryQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<RiskRegisterSummaryQuery, RiskRegisterSummaryDto>
{
    public Task<RiskRegisterSummaryDto> Handle(RiskRegisterSummaryQuery query, CancellationToken cancellationToken)
        => analytics.RiskSummaryAsync(cancellationToken);
}

// ---- Controls & Compliance analytics (ViewAnalytics) ----

public sealed record ControlEffectivenessQuery : IQuery<ControlEffectivenessSummaryDto>;

public sealed class ControlEffectivenessQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<ControlEffectivenessQuery, ControlEffectivenessSummaryDto>
{
    public Task<ControlEffectivenessSummaryDto> Handle(ControlEffectivenessQuery query, CancellationToken cancellationToken)
        => analytics.ControlEffectivenessAsync(cancellationToken);
}

public sealed record ComplianceByRegulationQuery : IQuery<IReadOnlyList<ComplianceByRegulationRowDto>>;

public sealed class ComplianceByRegulationQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<ComplianceByRegulationQuery, IReadOnlyList<ComplianceByRegulationRowDto>>
{
    public Task<IReadOnlyList<ComplianceByRegulationRowDto>> Handle(ComplianceByRegulationQuery query, CancellationToken cancellationToken)
        => analytics.ComplianceByRegulationAsync(cancellationToken);
}

public sealed record FindingFollowUpQuery : IQuery<FindingFollowUpSummaryDto>;

public sealed class FindingFollowUpQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<FindingFollowUpQuery, FindingFollowUpSummaryDto>
{
    public Task<FindingFollowUpSummaryDto> Handle(FindingFollowUpQuery query, CancellationToken cancellationToken)
        => analytics.FindingFollowUpAsync(cancellationToken);
}

public sealed record ProcedureSummaryQuery : IQuery<ProcedureSummaryDto>;

public sealed class ProcedureSummaryQueryHandler(IAnalyticsQueryService analytics)
    : IQueryHandler<ProcedureSummaryQuery, ProcedureSummaryDto>
{
    public Task<ProcedureSummaryDto> Handle(ProcedureSummaryQuery query, CancellationToken cancellationToken)
        => analytics.ProcedureSummaryAsync(cancellationToken);
}

// ---- Coverage matrix (reuse ICoverageQueryService — do NOT reimplement coverage) ----

public sealed record AnalyticsCoverageQuery(int WindowMonths) : IQuery<CoverageMatrix>;

public sealed class AnalyticsCoverageQueryHandler(ICoverageQueryService coverage)
    : IQueryHandler<AnalyticsCoverageQuery, CoverageMatrix>
{
    public Task<CoverageMatrix> Handle(AnalyticsCoverageQuery query, CancellationToken cancellationToken)
        => coverage.CoverageMatrixAsync(query.WindowMonths <= 0 ? 12 : query.WindowMonths, cancellationToken);
}

// ---- Performance scorecards (PerformanceAnalyticsView; in-handler self-coverage suppression) ----

public sealed record PerformanceScorecardsQuery : IQuery<IReadOnlyList<PerformanceScorecardDto>>;

public sealed class PerformanceScorecardsQueryHandler(
    IAnalyticsQueryService analytics, IPermissionResolver permissions, ICurrentUser currentUser)
    : IQueryHandler<PerformanceScorecardsQuery, IReadOnlyList<PerformanceScorecardDto>>
{
    public async Task<IReadOnlyList<PerformanceScorecardDto>> Handle(PerformanceScorecardsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException();
        }

        // Scorecards are gated by the controller attribute too; re-check in-handler so the dispatch path
        // (dashboard widget) is equally protected, and never lets a holder grade their own performance
        // unless they additionally hold the CIA oversight permission (self-coverage suppression).
        if (!await permissions.HasPermissionAsync(userId, PermissionKeys.PerformanceAnalyticsView, cancellationToken: cancellationToken))
        {
            throw new ForbiddenAccessException();
        }

        var scorecards = await analytics.PerformanceScorecardsAsync(cancellationToken);

        var canSeeSelf = await permissions.HasPermissionAsync(userId, PermissionKeys.Cia, cancellationToken: cancellationToken);
        return canSeeSelf
            ? scorecards
            : scorecards.Where(s => s.AuditLeadUserId != userId).ToArray();
    }
}
