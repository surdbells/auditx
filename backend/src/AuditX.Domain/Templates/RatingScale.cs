using AuditX.Domain.Common;

namespace AuditX.Domain.Templates;

/// <summary>
/// A bank-configurable, reusable labelled scale for <see cref="ResponseType.Rating"/> checklist items
/// (e.g. "1=Poor .. 5=Excellent"). A <see cref="TemplateItem"/>/<see cref="AuditChecklistItem"/> with response
/// type Rating references one by id in its <c>ResponseConfigJson</c>; the auditor picks a point's value when
/// responding, and that point's score drives post-response scoring. <see cref="PointsJson"/> is opaque to the
/// domain (parsed/validated in the application layer), mirroring <c>RiskDimension.ScaleLabelOverridesJson</c>.
/// </summary>
public sealed class RatingScale : AggregateRoot
{
    private RatingScale()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>JSON array of {value, label, score} points, e.g. <c>[{"value":1,"label":"Poor","score":0}, ...]</c>.</summary>
    public string PointsJson { get; private set; } = null!;

    public static RatingScale Create(string name, string? description, string pointsJson) => new()
    {
        Name = Guard.NotNullOrWhiteSpace(name, "rating_scale.name_required", "Rating scale name is required."),
        Description = description?.Trim(),
        PointsJson = Guard.NotNullOrWhiteSpace(pointsJson, "rating_scale.points_required", "At least one scale point is required."),
        IsActive = true,
    };

    public void Update(string? name, string? description, string? pointsJson, bool? isActive)
    {
        if (name is not null)
        {
            Name = Guard.NotNullOrWhiteSpace(name, "rating_scale.name_required", "Rating scale name is required.");
        }

        if (description is not null)
        {
            Description = description.Trim();
        }

        if (pointsJson is not null)
        {
            PointsJson = Guard.NotNullOrWhiteSpace(pointsJson, "rating_scale.points_required", "At least one scale point is required.");
        }

        if (isActive is { } active)
        {
            IsActive = active;
        }
    }
}
