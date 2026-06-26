namespace AuditX.Application.Abstractions.Universe;

/// <summary>Provides the bank's active taxonomies for validation and analytics axes (M3; M12 later versions these).</summary>
public interface ITaxonomyProvider
{
    Task<IReadOnlyList<string>> GetActiveEntityTypesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsEntityTypeActiveAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>Audit types in use across the bank (derived from templates / plan items), for the coverage matrix axis.</summary>
    Task<IReadOnlyList<string>> GetAuditTypesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Coverage analytics read model (US-M3-021..023). Read-only; no audit-trail side effects.</summary>
public interface ICoverageQueryService
{
    Task<IReadOnlyList<NotAuditedRow>> NotAuditedSinceAsync(int months, string? entityType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HighRiskGapRow>> HighRiskGapsAsync(int months, string? entityType, CancellationToken cancellationToken = default);

    Task<CoverageMatrix> CoverageMatrixAsync(int windowMonths, CancellationToken cancellationToken = default);
}

public sealed record NotAuditedRow(Guid Id, string EntityType, string Name, DateTimeOffset? LastAuditedAt);

public sealed record HighRiskGapRow(Guid Id, string EntityType, string Name, decimal? CompositeResidualScore, DateTimeOffset? LastAuditedAt);

public sealed record CoverageMatrix(IReadOnlyList<string> Rows, IReadOnlyList<string> Columns, int[][] Cells);
