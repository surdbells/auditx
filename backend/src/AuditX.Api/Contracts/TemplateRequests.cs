namespace AuditX.Api.Contracts;

public sealed record CreateTemplateRequest(string Name, string AuditType, string? Description);

public sealed record UpdateTemplateMetadataRequest(string Name, string? Description);

public sealed record TemplateItemRequest(
    string Prompt,
    string? ReferenceNotes,
    string ResponseType,
    string? SectionName,
    bool IsRequired,
    string? DefaultAssignmentRuleJson);

public sealed record ReorderItemsRequest(IReadOnlyList<Guid> OrderedItemIds);

public sealed record AddSectionRequest(string Name);

public sealed record RenameSectionRequest(string CurrentName, string NewName);

public sealed record CloneTemplateRequest(string NewName);
