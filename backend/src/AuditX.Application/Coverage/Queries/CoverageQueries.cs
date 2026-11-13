using AuditX.Application.Abstractions.Universe;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using FluentValidation;

namespace AuditX.Application.Coverage.Queries;

/// <summary>Entities not audited within the last N months (or never) — US-M3-021. <c>months</c> is required.</summary>
public sealed record NotAuditedSinceQuery(int Months, string? EntityType) : IQuery<IReadOnlyList<NotAuditedRow>>;

public sealed class NotAuditedSinceQueryValidator : AbstractValidator<NotAuditedSinceQuery>
{
    public NotAuditedSinceQueryValidator() => RuleFor(x => x.Months).GreaterThan(0);
}

public sealed class NotAuditedSinceQueryHandler(ICoverageQueryService coverage)
    : IQueryHandler<NotAuditedSinceQuery, IReadOnlyList<NotAuditedRow>>
{
    public Task<IReadOnlyList<NotAuditedRow>> Handle(NotAuditedSinceQuery query, CancellationToken cancellationToken)
        => coverage.NotAuditedSinceAsync(query.Months, query.EntityType, cancellationToken);
}

/// <summary>Top-quartile residual-risk entities lacking a recent audit — US-M3-022.</summary>
public sealed record HighRiskGapsQuery(int Months, string? EntityType) : IQuery<IReadOnlyList<HighRiskGapRow>>;

public sealed class HighRiskGapsQueryValidator : AbstractValidator<HighRiskGapsQuery>
{
    public HighRiskGapsQueryValidator() => RuleFor(x => x.Months).GreaterThan(0);
}

public sealed class HighRiskGapsQueryHandler(ICoverageQueryService coverage)
    : IQueryHandler<HighRiskGapsQuery, IReadOnlyList<HighRiskGapRow>>
{
    public Task<IReadOnlyList<HighRiskGapRow>> Handle(HighRiskGapsQuery query, CancellationToken cancellationToken)
        => coverage.HighRiskGapsAsync(query.Months, query.EntityType, cancellationToken);
}

/// <summary>
/// The full audit history of one audit-universe entity: every audit linked to it — planned, ad hoc and
/// self-assessments, whatever their status — with each one's checklist outcome, findings and issued reports, plus the
/// headline figures a reviewer wants first (how often it was really audited, what is still open).
/// </summary>
public sealed record EntityAuditHistoryQuery(Guid EntityId) : IQuery<EntityAuditHistoryDto>;

public sealed record EntityAuditHistoryDto(
    Guid EntityId, string EntityName, string EntityType, bool IsArchived, DateTimeOffset? LastAuditedAt, int? ExpectedAuditsPerYear,
    int TotalAudits, int CompletedAudits, int InFlightAudits, int SelfAssessments, int TotalFindings, int OpenFindings,
    IReadOnlyList<EntityAuditHistoryItemDto> Audits);

public sealed record EntityAuditHistoryItemDto(
    Guid AuditId, string Name, string AuditType, string Status, bool IsSelfAssessment,
    DateOnly StartDate, DateOnly TargetEndDate, DateOnly? ActualEndDate,
    Guid LeadUserId, string? LeadName, Guid AuditeeUserId, string? AuditeeName,
    // "plan" (launched from an annual plan item), "ad_hoc" or "self_assessment".
    string Origin, string? PlanPeriodLabel,
    int ChecklistItems, int RespondedItems, int Passed, int Failed, int NotApplicable,
    int Findings, int OpenFindings, int CriticalFindings, int HighFindings, int MediumFindings, int LowFindings,
    int ReportsIssued);

public sealed class EntityAuditHistoryQueryHandler(ICoverageQueryService coverage)
    : IQueryHandler<EntityAuditHistoryQuery, EntityAuditHistoryDto>
{
    public async Task<EntityAuditHistoryDto> Handle(EntityAuditHistoryQuery query, CancellationToken cancellationToken)
    {
        var history = await coverage.EntityAuditHistoryAsync(query.EntityId, cancellationToken)
            ?? throw new NotFoundException("Auditable entity", query.EntityId);

        var items = history.Audits.Select(a => new EntityAuditHistoryItemDto(
            a.AuditId, a.Name, a.AuditType, a.Status.ToSnake(), a.IsSelfAssessment, a.StartDate, a.TargetEndDate, a.ActualEndDate,
            a.LeadUserId, a.LeadName, a.AuditeeUserId, a.AuditeeName,
            a.IsSelfAssessment ? "self_assessment" : a.PlanItemId is null ? "ad_hoc" : "plan", a.PlanPeriodLabel,
            a.ChecklistItems, a.RespondedItems, a.Passed, a.Failed, a.NotApplicable,
            a.Findings, a.OpenFindings, a.CriticalFindings, a.HighFindings, a.MediumFindings, a.LowFindings,
            a.ReportsIssued)).ToArray();

        // Headline counts are about INDEPENDENT audits; self-assessments are listed but counted separately.
        var independent = history.Audits.Where(a => !a.IsSelfAssessment).ToArray();
        return new EntityAuditHistoryDto(
            history.EntityId, history.EntityName, history.EntityType, history.IsArchived, history.LastAuditedAt, history.ExpectedAuditsPerYear,
            independent.Length,
            independent.Count(a => a.Status == Domain.Enums.AuditStatus.Completed),
            independent.Count(a => a.Status is not (Domain.Enums.AuditStatus.Completed or Domain.Enums.AuditStatus.Cancelled)),
            history.Audits.Count(a => a.IsSelfAssessment),
            history.Audits.Sum(a => a.Findings),
            history.Audits.Sum(a => a.OpenFindings),
            items);
    }
}

/// <summary>Coverage matrix: completed audits per (entity type × audit type) within a window — US-M3-023.</summary>
public sealed record CoverageMatrixQuery(int WindowMonths) : IQuery<CoverageMatrix>;

public sealed class CoverageMatrixQueryHandler(ICoverageQueryService coverage)
    : IQueryHandler<CoverageMatrixQuery, CoverageMatrix>
{
    public Task<CoverageMatrix> Handle(CoverageMatrixQuery query, CancellationToken cancellationToken)
        => coverage.CoverageMatrixAsync(query.WindowMonths <= 0 ? 12 : query.WindowMonths, cancellationToken);
}
