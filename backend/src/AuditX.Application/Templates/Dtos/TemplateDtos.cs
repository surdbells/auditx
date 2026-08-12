namespace AuditX.Application.Templates.Dtos;

public sealed record TemplateItemDto(
    Guid Id,
    string Prompt,
    string? ReferenceNotes,
    string ResponseType,
    string? SectionName,
    int OrderIndex,
    bool IsRequired,
    string? DefaultAssignmentRuleJson,
    string? ResponseConfigJson,
    string? RiskRating,
    Guid? ControlId);

public sealed record RatingScaleDto(Guid Id, string Name, string? Description, bool IsActive, string PointsJson);

public sealed record TemplateSectionDto(Guid Id, string Name, int OrderIndex);

public sealed record TemplateVersionSummaryDto(Guid Id, int VersionNumber, DateTimeOffset PublishedAt);

public sealed record TemplateDto(
    Guid Id,
    string Name,
    string AuditType,
    string Description,
    string Status,
    int CurrentVersion,
    Guid? ClonedFromTemplateId,
    IReadOnlyList<TemplateItemDto> Items,
    IReadOnlyList<TemplateSectionDto> Sections,
    IReadOnlyList<TemplateVersionSummaryDto> Versions);

/// <summary>Lightweight projection for list views (no items/sections).</summary>
public sealed record TemplateListItemDto(
    Guid Id,
    string Name,
    string AuditType,
    string Description,
    string Status,
    int CurrentVersion,
    int ItemCount);

public sealed record TemplateItemSnapshotDto(
    string Prompt,
    string? ReferenceNotes,
    string ResponseType,
    string? SectionName,
    int OrderIndex,
    bool IsRequired,
    string? DefaultAssignmentRuleJson,
    string? ResponseConfigJson,
    string? RiskRating,
    Guid? ControlId);

public sealed record TemplateVersionDetailDto(
    int VersionNumber,
    DateTimeOffset PublishedAt,
    IReadOnlyList<TemplateItemSnapshotDto> Items);

/// <summary>Structured diff between two published versions (US-M2-013).</summary>
public sealed record TemplateDiffDto(
    int FromVersion,
    int ToVersion,
    IReadOnlyList<TemplateItemSnapshotDto> Added,
    IReadOnlyList<TemplateItemSnapshotDto> Removed,
    IReadOnlyList<TemplateDiffModifiedDto> Modified);

public sealed record TemplateDiffModifiedDto(string Prompt, TemplateItemSnapshotDto Before, TemplateItemSnapshotDto After);

/// <summary>Outcome of a publish: applied (<see cref="Template"/>) or held by a maker-checker gate.</summary>
public sealed record TemplateActionResult(TemplateDto? Template, Guid? PendingActionId)
{
    public bool IsPending => PendingActionId is not null;
}
