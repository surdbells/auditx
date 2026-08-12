namespace AuditX.Api.Contracts;

public sealed record CreateTemplateRequest(string Name, string AuditType, string? Description);

public sealed record UpdateTemplateMetadataRequest(string Name, string? Description);

public sealed record TemplateItemRequest(
    string Prompt,
    string? ReferenceNotes,
    string ResponseType,
    string? SectionName,
    bool IsRequired,
    string? DefaultAssignmentRuleJson,
    string? ResponseConfigJson = null,
    string? RiskRating = null,
    Guid? ControlId = null);

public sealed record CreateRatingScaleRequest(string Name, string? Description, string PointsJson);

public sealed record UpdateRatingScaleRequest(string? Name, string? Description, string? PointsJson, bool? IsActive);

public sealed record ReorderItemsRequest(IReadOnlyList<Guid> OrderedItemIds);

public sealed record AddSectionRequest(string Name);

public sealed record RenameSectionRequest(string CurrentName, string NewName);

public sealed record ReorderSectionsRequest(IReadOnlyList<string> OrderedSectionNames);

public sealed record CloneTemplateRequest(string NewName);
