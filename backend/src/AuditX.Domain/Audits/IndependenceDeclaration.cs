using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>
/// One team member's declaration that they are independent of the area an audit examines, or their disclosure of a
/// conflict of interest. A disclosed conflict stands until a manager (never the declarant) accepts it and records the
/// safeguards; until then the declarant may not answer or sign off the audit's working papers. A member whose
/// circumstances change declares again, which replaces the earlier declaration and its decision.
/// Child of the <see cref="Audit"/> aggregate; declarations are evidence of independence, so they are never deleted.
/// </summary>
public sealed class IndependenceDeclaration : Entity, IBelongsToAggregate
{
    public const int MaxLength = 2000;

    private IndependenceDeclaration()
    {
    }

    internal IndependenceDeclaration(Guid auditId, Guid userId, bool hasConflict, string? conflictDetails, DateTimeOffset nowUtc)
    {
        AuditId = auditId;
        UserId = userId;
        Apply(hasConflict, conflictDetails, nowUtc);
    }

    public Guid AuditId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AuditId;

    /// <summary>The team member who declared.</summary>
    public Guid UserId { get; private set; }

    public IndependenceStatus Status { get; private set; }

    public bool HasConflict { get; private set; }

    /// <summary>What the conflict is (a relationship, a prior role in the area, a financial interest). Null when none.</summary>
    public string? ConflictDetails { get; private set; }

    public DateTimeOffset DeclaredAt { get; private set; }

    /// <summary>The manager who accepted a disclosed conflict.</summary>
    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>The safeguards the manager put in place when accepting the conflict.</summary>
    public string? ReviewNote { get; private set; }

    /// <summary>True when the declarant may work the audit: no conflict, or a conflict a manager accepted.</summary>
    public bool IsCleared => Status is IndependenceStatus.Declared or IndependenceStatus.ConflictAccepted;

    internal void Redeclare(bool hasConflict, string? conflictDetails, DateTimeOffset nowUtc) => Apply(hasConflict, conflictDetails, nowUtc);

    internal void AcceptConflict(Guid acceptedBy, string safeguards, DateTimeOffset nowUtc)
    {
        if (Status != IndependenceStatus.ConflictDisclosed)
        {
            throw new InvalidStateTransitionException("audit.declaration_not_pending", "Only a disclosed conflict that is awaiting a decision can be accepted.");
        }

        if (acceptedBy == UserId)
        {
            throw new DomainException("audit.accept_own_conflict", "Someone other than the person who disclosed the conflict must decide on it.");
        }

        ReviewNote = Text(safeguards, "audit.safeguards_required", "Record the safeguards that make the conflict acceptable.");
        ReviewedBy = acceptedBy;
        ReviewedAt = nowUtc;
        Status = IndependenceStatus.ConflictAccepted;
    }

    private void Apply(bool hasConflict, string? conflictDetails, DateTimeOffset nowUtc)
    {
        HasConflict = hasConflict;
        ConflictDetails = hasConflict
            ? Text(conflictDetails, "audit.conflict_details_required", "Describe the conflict of interest so a manager can decide on it.")
            : null;
        Status = hasConflict ? IndependenceStatus.ConflictDisclosed : IndependenceStatus.Declared;
        DeclaredAt = nowUtc;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewNote = null;
    }

    private static string Text(string? value, string code, string message)
    {
        var trimmed = Guard.NotNullOrWhiteSpace(value, code, message).Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainException("audit.declaration_too_long", $"Keep it to {MaxLength} characters or fewer.");
        }

        return trimmed;
    }
}
