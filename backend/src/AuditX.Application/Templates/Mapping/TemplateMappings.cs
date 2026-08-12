using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Json;
using AuditX.Application.Templates.Dtos;
using AuditX.Domain.Templates;

namespace AuditX.Application.Templates.Mapping;

/// <summary>Explicit mappers for the Template aggregate (see ADR-0001).</summary>
public static class TemplateMappings
{
    public static TemplateDto ToDto(this Template template) => new(
        template.Id,
        template.Name,
        template.AuditType,
        template.Description,
        template.Status.ToSnake(),
        template.CurrentVersion,
        template.ClonedFromTemplateId,
        template.Items.OrderBy(i => i.OrderIndex).Select(ToDto).ToArray(),
        template.Sections.OrderBy(s => s.OrderIndex).Select(s => new TemplateSectionDto(s.Id, s.Name, s.OrderIndex)).ToArray(),
        template.Versions.OrderBy(v => v.VersionNumber).Select(v => new TemplateVersionSummaryDto(v.Id, v.VersionNumber, v.PublishedAt)).ToArray());

    public static TemplateListItemDto ToListItemDto(this Template template) => new(
        template.Id, template.Name, template.AuditType, template.Description,
        template.Status.ToSnake(), template.CurrentVersion, template.Items.Count);

    public static TemplateItemDto ToDto(this TemplateItem item) => new(
        item.Id, item.Prompt, item.ReferenceNotes, item.ResponseType.ToSnake(),
        item.SectionName, item.OrderIndex, item.IsRequired, item.DefaultAssignmentRuleJson,
        item.ResponseConfigJson, item.RiskRating?.ToSnake());

    public static RatingScaleDto ToDto(this RatingScale scale) => new(scale.Id, scale.Name, scale.Description, scale.IsActive, scale.PointsJson);

    public static TemplateVersionDetailDto ToDetailDto(this TemplateVersion version)
    {
        var snapshot = AppJson.Deserialize<List<TemplateItemSnapshot>>(version.ItemsSnapshotJson);
        return new TemplateVersionDetailDto(
            version.VersionNumber,
            version.PublishedAt,
            snapshot.Select(ToSnapshotDto).ToArray());
    }

    public static TemplateItemSnapshotDto ToSnapshotDto(this TemplateItemSnapshot snapshot) => new(
        snapshot.Prompt, snapshot.ReferenceNotes, snapshot.ResponseType.ToSnake(),
        snapshot.SectionName, snapshot.OrderIndex, snapshot.IsRequired, snapshot.DefaultAssignmentRuleJson,
        snapshot.ResponseConfigJson, snapshot.RiskRating?.ToSnake());

    /// <summary>Serializer used by the aggregate when snapshotting items at publish time.</summary>
    public static string SerializeSnapshot(IReadOnlyList<TemplateItemSnapshot> snapshot) => AppJson.Serialize(snapshot);
}
