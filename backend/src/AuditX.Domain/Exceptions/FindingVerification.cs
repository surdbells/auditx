using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Exceptions;

/// <summary>
/// A post-closure follow-up verification that a finding's remediation is effective (P2-B). Child of the
/// AuditException aggregate; a finding may accrue several over time (each closure can be re-verified).
/// </summary>
public sealed class FindingVerification : Entity, IBelongsToAggregate
{
    private FindingVerification()
    {
    }

    public Guid ExceptionId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => ExceptionId;

    public VerificationResult Result { get; private set; }

    public Guid VerifiedByUserId { get; private set; }

    public DateTimeOffset VerifiedAt { get; private set; }

    public string? Notes { get; private set; }

    internal FindingVerification(Guid exceptionId, VerificationResult result, Guid verifiedByUserId, string? notes, DateTimeOffset verifiedAt)
    {
        ExceptionId = exceptionId;
        Result = result;
        VerifiedByUserId = verifiedByUserId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        VerifiedAt = verifiedAt;
    }
}
