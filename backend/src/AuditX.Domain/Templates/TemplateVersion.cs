using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Templates;

/// <summary>An immutable snapshot of a template's items at the moment it was published (US-M2-008/012).</summary>
public sealed class TemplateVersion : Entity
{
    private TemplateVersion()
    {
    }

    public Guid TemplateId { get; private set; }

    public int VersionNumber { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>JSON array of <see cref="TemplateItemSnapshot"/> captured at publish time.</summary>
    public string ItemsSnapshotJson { get; private set; } = null!;

    internal TemplateVersion(Guid templateId, int versionNumber, DateTimeOffset publishedAt, string itemsSnapshotJson)
    {
        TemplateId = templateId;
        VersionNumber = versionNumber;
        PublishedAt = publishedAt;
        ItemsSnapshotJson = itemsSnapshotJson;
    }
}

/// <summary>The shape of a single item inside a published version snapshot.</summary>
public sealed record TemplateItemSnapshot(
    string Prompt,
    string? ReferenceNotes,
    ResponseType ResponseType,
    string? SectionName,
    int OrderIndex,
    bool IsRequired,
    string? DefaultAssignmentRuleJson,
    string? ResponseConfigJson = null,
    ExceptionSeverity? RiskRating = null);
