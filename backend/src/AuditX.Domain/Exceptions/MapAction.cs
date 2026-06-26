using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Exceptions;

/// <summary>A single remediation action within a Management Action Plan (M6). Child of the AuditException aggregate.</summary>
public sealed class MapAction : Entity, IBelongsToAggregate
{
    private MapAction()
    {
    }

    public Guid ExceptionId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => ExceptionId;

    public string Description { get; private set; } = null!;

    public Guid OwnerUserId { get; private set; }

    public DateOnly TargetDate { get; private set; }

    public string? ExpectedEvidenceType { get; private set; }

    public MapActionStatus Status { get; private set; } = MapActionStatus.Pending;

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? CompletedBy { get; private set; }

    internal MapAction(Guid exceptionId, string description, Guid ownerUserId, DateOnly targetDate, string? expectedEvidenceType)
    {
        ExceptionId = exceptionId;
        Description = Guard.NotNullOrWhiteSpace(description, "exception.map_action_description_required", "A remediation action description is required.");
        OwnerUserId = ownerUserId;
        TargetDate = targetDate;
        ExpectedEvidenceType = expectedEvidenceType;
    }

    internal void MarkComplete(Guid completedBy, DateTimeOffset completedAt)
    {
        Status = MapActionStatus.Complete;
        CompletedAt = completedAt;
        CompletedBy = completedBy;
    }
}
