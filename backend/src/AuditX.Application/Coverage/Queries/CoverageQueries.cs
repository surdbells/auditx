using AuditX.Application.Abstractions.Universe;
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

/// <summary>Coverage matrix: completed audits per (entity type × audit type) within a window — US-M3-023.</summary>
public sealed record CoverageMatrixQuery(int WindowMonths) : IQuery<CoverageMatrix>;

public sealed class CoverageMatrixQueryHandler(ICoverageQueryService coverage)
    : IQueryHandler<CoverageMatrixQuery, CoverageMatrix>
{
    public Task<CoverageMatrix> Handle(CoverageMatrixQuery query, CancellationToken cancellationToken)
        => coverage.CoverageMatrixAsync(query.WindowMonths <= 0 ? 12 : query.WindowMonths, cancellationToken);
}
