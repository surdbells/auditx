using AuditX.Domain.ReferenceData;

namespace AuditX.Application.ReferenceData;

/// <summary>A managed reference-data item as returned to clients.</summary>
public sealed record ReferenceDataItemDto(Guid Id, string Category, string Code, string Label, string? Description, int SortOrder, bool IsActive);

/// <summary>A manageable reference-data category descriptor (for the admin UI's list menu).</summary>
public sealed record ReferenceDataCategoryDto(string Code, string Label);

public static class ReferenceDataMappings
{
    public static ReferenceDataItemDto ToDto(this ReferenceDataItem item) =>
        new(item.Id, item.Category, item.Code, item.Label, item.Description, item.SortOrder, item.IsActive);

    public static ReferenceDataCategoryDto ToDto(this ReferenceDataCategoryDescriptor descriptor) =>
        new(descriptor.Code, descriptor.Label);
}
