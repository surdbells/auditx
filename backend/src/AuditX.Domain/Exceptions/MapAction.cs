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

    /// <summary>The last reminder stage the daily job sent for this action (each stage is sent once).</summary>
    public MapActionReminderStage ReminderStage { get; private set; } = MapActionReminderStage.None;

    public DateTimeOffset? LastRemindedAt { get; private set; }

    /// <summary>True while the action still counts towards the current plan (not replaced by a reopen).</summary>
    public bool IsCurrent => Status != MapActionStatus.Superseded;

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

    internal void Supersede() => Status = MapActionStatus.Superseded;

    /// <summary>Moves the due date after an approved extension and starts the reminder sequence again.</summary>
    internal void Reschedule(DateOnly newTargetDate)
    {
        TargetDate = newTargetDate;
        ReminderStage = MapActionReminderStage.None;
        LastRemindedAt = null;
    }

    internal void MarkReminded(MapActionReminderStage stage, DateTimeOffset at)
    {
        ReminderStage = stage;
        LastRemindedAt = at;
    }
}
