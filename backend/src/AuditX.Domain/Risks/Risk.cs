using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Risks;

/// <summary>
/// An enterprise risk-register entry (P1-A). Carries a likelihood×impact pair (1–5 each) for both the inherent
/// and — once assessed — the residual (post-treatment) position, an owner, a treatment strategy and a lifecycle.
/// The likelihood/impact split is what enables a true 5×5 heatmap; the residual position (falling back to inherent
/// when unassessed) is the "current" score the register and heatmap report on.
///
/// Optionally linked to an audit-universe entity, from which it inherits an org unit for department roll-ups.
/// Soft-deletable so an erroneous entry can be removed while preserving the audit trail; a legitimately-retired
/// risk is <see cref="RiskStatus.Closed"/> (terminal) instead.
/// </summary>
public sealed class Risk : AggregateRoot, ISoftDeletable
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    private Risk()
    {
    }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public string Category { get; private set; } = null!;

    public Guid OwnerUserId { get; private set; }

    /// <summary>Optional link to the auditable entity this risk sits on (inherits its org unit).</summary>
    public Guid? AuditableEntityId { get; private set; }

    public int InherentLikelihood { get; private set; }

    public int InherentImpact { get; private set; }

    public int? ResidualLikelihood { get; private set; }

    public int? ResidualImpact { get; private set; }

    public RiskTreatmentStrategy? TreatmentStrategy { get; private set; }

    public string? TreatmentPlan { get; private set; }

    public RiskStatus Status { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public DateOnly? NextReviewDate { get; private set; }

    public DateTimeOffset IdentifiedAt { get; private set; }

    public Guid IdentifiedByUserId { get; private set; }

    public string? ClosureRationale { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public int InherentScore => InherentLikelihood * InherentImpact;

    public int? ResidualScore => ResidualLikelihood is { } l && ResidualImpact is { } i ? l * i : null;

    /// <summary>The score the heatmap/register reports on: residual when assessed, otherwise inherent.</summary>
    public int CurrentScore => ResidualScore ?? InherentScore;

    public int CurrentLikelihood => ResidualLikelihood ?? InherentLikelihood;

    public int CurrentImpact => ResidualImpact ?? InherentImpact;

    public bool IsClosed => Status == RiskStatus.Closed;

    public static Risk Register(
        string title, string? description, string category, Guid ownerUserId, Guid? auditableEntityId,
        int inherentLikelihood, int inherentImpact, DateOnly? targetDate, Guid identifiedBy, DateTimeOffset nowUtc)
    {
        ValidateRating(inherentLikelihood, "risk.likelihood_range");
        ValidateRating(inherentImpact, "risk.impact_range");
        Guard.Against(ownerUserId == Guid.Empty, "risk.owner_required", "An owner is required.");

        return new Risk
        {
            Title = Guard.NotNullOrWhiteSpace(title, "risk.title_required", "A title is required.").Trim(),
            Description = Normalise(description),
            Category = Guard.NotNullOrWhiteSpace(category, "risk.category_required", "A category is required.").Trim(),
            OwnerUserId = ownerUserId,
            AuditableEntityId = auditableEntityId,
            InherentLikelihood = inherentLikelihood,
            InherentImpact = inherentImpact,
            Status = RiskStatus.Open,
            TargetDate = targetDate,
            IdentifiedAt = nowUtc,
            IdentifiedByUserId = identifiedBy,
        };
    }

    /// <summary>
    /// Updates the metadata, the inherent rating and — optionally — the residual rating + treatment. Supplying a
    /// residual (both likelihood and impact) auto-advances an Open risk to Assessed. The residual position may not
    /// exceed the inherent position (treatment reduces, never raises, risk).
    /// </summary>
    public void Update(
        string title, string? description, string category, Guid ownerUserId, Guid? auditableEntityId,
        int inherentLikelihood, int inherentImpact, int? residualLikelihood, int? residualImpact,
        RiskTreatmentStrategy? treatmentStrategy, string? treatmentPlan, DateOnly? targetDate, DateOnly? nextReviewDate)
    {
        EnsureNotClosed();
        ValidateRating(inherentLikelihood, "risk.likelihood_range");
        ValidateRating(inherentImpact, "risk.impact_range");
        Guard.Against(ownerUserId == Guid.Empty, "risk.owner_required", "An owner is required.");

        if ((residualLikelihood is null) != (residualImpact is null))
        {
            throw new DomainException("risk.residual_incomplete", "Residual likelihood and impact must both be set together.");
        }

        if (residualLikelihood is { } rl && residualImpact is { } ri)
        {
            ValidateRating(rl, "risk.likelihood_range");
            ValidateRating(ri, "risk.impact_range");
            Guard.Against(rl * ri > inherentLikelihood * inherentImpact, "risk.residual_exceeds_inherent", "Residual risk cannot exceed inherent risk.");
        }

        Title = Guard.NotNullOrWhiteSpace(title, "risk.title_required", "A title is required.").Trim();
        Description = Normalise(description);
        Category = Guard.NotNullOrWhiteSpace(category, "risk.category_required", "A category is required.").Trim();
        OwnerUserId = ownerUserId;
        AuditableEntityId = auditableEntityId;
        InherentLikelihood = inherentLikelihood;
        InherentImpact = inherentImpact;
        ResidualLikelihood = residualLikelihood;
        ResidualImpact = residualImpact;
        TreatmentStrategy = treatmentStrategy;
        TreatmentPlan = Normalise(treatmentPlan);
        TargetDate = targetDate;
        NextReviewDate = nextReviewDate;

        if (residualLikelihood is not null && Status == RiskStatus.Open)
        {
            Status = RiskStatus.Assessed;
        }
    }

    /// <summary>
    /// Moves the risk through its lifecycle. Any non-closed status may move to any other; Closed is terminal and
    /// requires a rationale. Closing stamps the actor + timestamp.
    /// </summary>
    public void ChangeStatus(RiskStatus target, string? rationale, Guid actorUserId, DateTimeOffset nowUtc)
    {
        EnsureNotClosed();
        if (target == Status)
        {
            return;
        }

        if (target == RiskStatus.Closed)
        {
            ClosureRationale = Guard.MinLength(rationale, 10, "risk.closure_rationale_required", "A closure rationale of at least 10 characters is required.").Trim();
            ClosedByUserId = actorUserId;
            ClosedAt = nowUtc;
        }

        Status = target;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private void EnsureNotClosed()
    {
        if (Status == RiskStatus.Closed)
        {
            throw new InvalidStateTransitionException("risk.closed", "A closed risk cannot be modified.");
        }
    }

    private static void ValidateRating(int value, string code)
        => Guard.Against(value < MinRating || value > MaxRating, code, "Likelihood and impact must be between 1 and 5.");

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
