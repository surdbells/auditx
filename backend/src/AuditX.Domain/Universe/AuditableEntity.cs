using System.Text.Json;
using AuditX.Domain.Common;
using AuditX.Domain.Universe.Events;

namespace AuditX.Domain.Universe;

/// <summary>
/// An auditable entity in the bank's audit universe (M3): a branch, process, system, vendor or product.
/// Carries inherent and residual risk scores per configured dimension and the derived composite scores.
/// Entities form an acyclic hierarchy (parent check is performed by the application layer, which has the
/// graph). "Archiving" is a soft delete.
/// </summary>
public sealed class AuditableEntity : AggregateRoot, ISoftDeletable
{
    private AuditableEntity()
    {
    }

    public string EntityType { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid? ParentEntityId { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string InherentRiskScoresJson { get; private set; } = "{}";

    public string ResidualRiskScoresJson { get; private set; } = "{}";

    public decimal? CompositeInherentScore { get; private set; }

    public decimal? CompositeResidualScore { get; private set; }

    public DateTimeOffset? LastAuditedAt { get; private set; }

    /// <summary>Optimistic-concurrency token (rowversion) for safe concurrent edits (US-M3-004).</summary>
    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public IReadOnlyDictionary<string, int> InherentScores => Deserialize(InherentRiskScoresJson);

    public IReadOnlyDictionary<string, int> ResidualScores => Deserialize(ResidualRiskScoresJson);

    public static AuditableEntity Create(string entityType, string name, string? description, Guid? parentEntityId, Guid? ownerUserId)
    {
        var entity = new AuditableEntity
        {
            EntityType = Guard.NotNullOrWhiteSpace(entityType, "universe.entity_type_required", "Entity type is required."),
            Name = Guard.NotNullOrWhiteSpace(name, "universe.name_required", "Entity name is required."),
            Description = description,
            ParentEntityId = parentEntityId,
            OwnerUserId = ownerUserId,
        };
        entity.RaiseDomainEvent(new EntityCreatedEvent(entity.Id, entity.Name, entity.EntityType));
        return entity;
    }

    public void UpdateDetails(string name, string entityType, string? description, Guid? ownerUserId)
    {
        Name = Guard.NotNullOrWhiteSpace(name, "universe.name_required", "Entity name is required.");
        EntityType = Guard.NotNullOrWhiteSpace(entityType, "universe.entity_type_required", "Entity type is required.");
        Description = description;
        OwnerUserId = ownerUserId;
    }

    /// <summary>Set the parent. Acyclicity must be verified by the caller (it owns the graph).</summary>
    public void SetParent(Guid? parentEntityId)
    {
        if (parentEntityId == Id)
        {
            throw new DomainException("universe.cycle_detected", "An entity cannot be its own parent.");
        }

        ParentEntityId = parentEntityId;
    }

    /// <summary>
    /// Apply a complete set of inherent and/or residual scores and recompute the composites. Each side
    /// supplied must cover every active dimension exactly (no unknown dimensions, all values in range).
    /// </summary>
    public void ApplyRiskScores(
        IReadOnlyDictionary<string, int>? inherent,
        IReadOnlyDictionary<string, int>? residual,
        IReadOnlyList<RiskDimensionSpec> activeDimensions)
    {
        if (inherent is null && residual is null)
        {
            throw new DomainException("universe.no_scores", "Provide inherent and/or residual scores.");
        }

        var beforeInherent = CompositeInherentScore;
        var beforeResidual = CompositeResidualScore;

        if (inherent is not null)
        {
            Validate(inherent, activeDimensions);
            InherentRiskScoresJson = Serialize(inherent);
            CompositeInherentScore = CompositeScore.Compute(inherent, activeDimensions);
        }

        if (residual is not null)
        {
            Validate(residual, activeDimensions);
            ResidualRiskScoresJson = Serialize(residual);
            CompositeResidualScore = CompositeScore.Compute(residual, activeDimensions);
        }

        RaiseDomainEvent(new EntityRiskScoredEvent(Id, beforeInherent, beforeResidual, CompositeInherentScore, CompositeResidualScore));
    }

    /// <summary>Record that an audit of this entity completed (drives coverage analytics). Cancellation must not call this.</summary>
    public void MarkAudited(DateTimeOffset auditCompletedAtUtc) => LastAuditedAt = auditCompletedAtUtc;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private static void Validate(IReadOnlyDictionary<string, int> scores, IReadOnlyList<RiskDimensionSpec> activeDimensions)
    {
        var activeNames = activeDimensions.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var key in scores.Keys)
        {
            if (!activeNames.Contains(key))
            {
                throw new DomainException("universe.unknown_dimension", $"'{key}' is not an active risk dimension.");
            }
        }

        foreach (var dimension in activeDimensions)
        {
            if (!scores.TryGetValue(dimension.Name, out var value))
            {
                throw new DomainException("universe.incomplete_scores", $"A score for dimension '{dimension.Name}' is required.");
            }

            if (value < dimension.ScaleMin || value > dimension.ScaleMax)
            {
                throw new DomainException("universe.score_out_of_range", $"Score for '{dimension.Name}' must be between {dimension.ScaleMin} and {dimension.ScaleMax}.");
            }
        }
    }

    private static string Serialize(IReadOnlyDictionary<string, int> scores) => JsonSerializer.Serialize(scores);

    private static IReadOnlyDictionary<string, int> Deserialize(string json)
        => JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
}
