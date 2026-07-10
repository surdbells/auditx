using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Analytics;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Ac;

namespace AuditX.Application.Ac.Generation;

/// <summary>
/// Composes an <see cref="AcPackComposition"/> from the M9 analytics read model at generation time (M13). Sources:
/// material findings, plan status, exception portfolio, sanctions consistency and recurrence clusters — ALL via the
/// <see cref="IAnalyticsQueryService"/> port plus the recurrence-cluster repository.
///
/// SCOPE GUARD (FR-M7-010 / NFR-SEC-007): the sanctions section is sourced ONLY from
/// <see cref="IAnalyticsQueryService.SanctionsConsistencyAsync"/>, whose DTO physically omits the subject id — the
/// assembler never touches raw sanctions cases. The full (unfiltered) material-findings list is snapshotted; the
/// restricted-visibility allow-list is applied per-requester at read, never baked into the snapshot.
/// </summary>
public sealed class AcPackContentAssembler(IAnalyticsQueryService analytics, IRecurrenceClusterRepository clusters, IClock clock)
{
    public async Task<AcPackComposition> AssembleAsync(AcPack pack, CancellationToken cancellationToken)
    {
        var planStatus = await analytics.PlanStatusAsync(cancellationToken);
        var portfolio = await analytics.ExceptionPortfolioAsync(cancellationToken);
        var materialFindings = await analytics.MaterialFindingsAsync(cancellationToken);
        var sanctions = await analytics.SanctionsConsistencyAsync(cancellationToken);
        var recurrencePage = await clusters.ListPagedAsync(new Common.Models.PageSpec(1, 100), cancellationToken);

        return new AcPackComposition(
            pack.PeriodStart,
            pack.PeriodEnd,
            pack.AcMeetingLabel,
            pack.VersionNumber,
            planStatus.TotalPlans,
            planStatus.TotalItems,
            planStatus.Completed,
            planStatus.CompletionPercent,
            portfolio.TotalOpen,
            portfolio.AverageClosureDays,
            portfolio.BySeverity.Select(s => new AcSeverityCountLine(s.Severity, s.Count)).ToArray(),
            materialFindings.Select(f => new AcMaterialFindingLine(
                f.ExceptionId, f.AuditId, f.Title, f.Severity, f.Status, f.AuditableEntityId, f.RaisedAt, f.TargetDate)).ToArray(),
            sanctions.TotalCases,
            sanctions.OverallGridAdherencePercent,
            sanctions.OverallAppealRatePercent,
            sanctions.ByBusinessUnit.Select(r => new AcSanctionsConsistencyLine(
                r.BusinessUnit, r.CaseCount, r.WithinGridCount, r.GridAdherencePercent, r.DeviationCount, r.AppealCount, r.AppealRatePercent)).ToArray(),
            recurrencePage.Items.Select(c => new AcRecurrenceClusterLine(
                c.Id, c.AuditableEntityId, c.Category, c.ClosedExceptionCount, c.WindowMonths, c.FirstOccurredAt, c.LastOccurredAt)).ToArray(),
            clock.UtcNow);
    }
}
