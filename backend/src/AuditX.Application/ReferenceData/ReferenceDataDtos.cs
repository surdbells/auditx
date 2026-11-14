using AuditX.Domain.ReferenceData;

namespace AuditX.Application.ReferenceData;

/// <summary>
/// A managed reference-data item as returned to clients. <see cref="LabelHistory"/> lists every label it has carried
/// (empty when never renamed) so a client shows each record with the label current when that record was created.
/// </summary>
public sealed record ReferenceDataItemDto(
    Guid Id, string Category, string Code, string Label, string? Description, int SortOrder, bool IsActive,
    IReadOnlyList<ReferenceDataLabelVersionDto>? LabelHistory = null);

/// <summary>One label a value has carried, from <see cref="EffectiveFrom"/> (MinValue for the original label).</summary>
public sealed record ReferenceDataLabelVersionDto(string Label, DateTimeOffset EffectiveFrom);

/// <summary>
/// A manageable reference-data category descriptor (for the admin UI's list menu): what the list is used for and
/// how many items it currently holds, so an empty or sparsely-populated list is visible before it is opened.
/// </summary>
public sealed record ReferenceDataCategoryDto(string Code, string Label, string Description, int ActiveCount, int InactiveCount);

/// <summary>Item counts for one category, split by active / archived.</summary>
public sealed record ReferenceDataCategoryCounts(int Active, int Inactive);

public static class ReferenceDataMappings
{
    public static ReferenceDataItemDto ToDto(this ReferenceDataItem item) =>
        new(item.Id, item.Category, item.Code, item.Label, item.Description, item.SortOrder, item.IsActive,
            item.LabelVersions.OrderBy(v => v.EffectiveFrom).Select(v => new ReferenceDataLabelVersionDto(v.Label, v.EffectiveFrom)).ToArray());

    public static ReferenceDataCategoryDto ToDto(this ReferenceDataCategoryDescriptor descriptor, ReferenceDataCategoryCounts? counts = null) =>
        new(descriptor.Code, descriptor.Label, descriptor.Description, counts?.Active ?? 0, counts?.Inactive ?? 0);
}
