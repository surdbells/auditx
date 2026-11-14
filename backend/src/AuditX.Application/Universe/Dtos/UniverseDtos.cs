namespace AuditX.Application.Universe.Dtos;

public sealed record EntityDto(
    Guid Id,
    string EntityType,
    string Name,
    string? Description,
    Guid? ParentEntityId,
    Guid? OwnerUserId,
    Guid? OrgUnitId,
    IReadOnlyDictionary<string, int> InherentScores,
    IReadOnlyDictionary<string, int> ResidualScores,
    decimal? CompositeInherentScore,
    decimal? CompositeResidualScore,
    DateTimeOffset? LastAuditedAt,
    int? ExpectedAuditsPerYear,
    string Version,
    bool IsArchived = false,
    DateTimeOffset? ArchivedAt = null,
    // When the record was created — reference labels on it are shown as they read at that time.
    DateTimeOffset? CreatedAt = null);

public sealed record RiskDimensionDto(
    Guid Id, string Name, decimal Weight, int ScaleMin, int ScaleMax, bool IsActive, string? ScaleLabelOverridesJson);

public sealed record BulkImportResultDto(int Created, IReadOnlyList<BulkImportErrorDto> Errors);

public sealed record BulkImportErrorDto(int Row, string Field, string Message);
