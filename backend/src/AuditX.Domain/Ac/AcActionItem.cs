using AuditX.Domain.Ac.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Ac;

/// <summary>
/// An Audit-Committee action item (M13). Lifecycle: <c>open → in_progress → closed</c> (closure REQUIRES a
/// non-blank response) and then the AC chair acknowledges → <c>acknowledged_closed</c> (terminal). Soft-deletable.
/// </summary>
public sealed class AcActionItem : AggregateRoot, ISoftDeletable
{
    private AcActionItem()
    {
    }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public AcActionItemStatus Status { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>The closure narrative (and any evidence summary) recorded when the item is closed. Required to close.</summary>
    public string? ClosureResponse { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public Guid? AcknowledgedByUserId { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public string? DeletionReason { get; private set; }

    public byte[] Version { get; private set; } = [];

    /// <summary>Create an open action item (raised by an AC member). Raises <see cref="AcActionItemCreatedEvent"/>.</summary>
    public static AcActionItem Create(string title, string? description, Guid? assignedToUserId, DateOnly? dueDate, Guid createdBy)
    {
        var item = new AcActionItem
        {
            Title = Guard.NotNullOrWhiteSpace(title, "ac_action_item.title_required", "An action-item title is required."),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = AcActionItemStatus.Open,
            AssignedToUserId = assignedToUserId is { } a && a != Guid.Empty ? a : null,
            DueDate = dueDate,
            CreatedByUserId = createdBy,
        };
        item.RaiseDomainEvent(new AcActionItemCreatedEvent(item.Id, item.Title, createdBy));
        return item;
    }

    /// <summary>Move open → in_progress.</summary>
    public void MarkInProgress() => Transition(AcActionItemStatus.InProgress, AcActionItemStatus.Open);

    /// <summary>
    /// Close the item with a REQUIRED closure response (open/in_progress → closed). A blank response throws a
    /// <see cref="DomainException"/> (BR — closure must record what was done).
    /// </summary>
    public void Close(string closureResponse, Guid closedBy, DateTimeOffset nowUtc)
    {
        Transition(AcActionItemStatus.Closed, AcActionItemStatus.Open, AcActionItemStatus.InProgress);
        ClosureResponse = Guard.NotNullOrWhiteSpace(
            closureResponse, "ac_action_item.closure_response_required", "A closure response is required to close an action item.");
        ClosedAt = nowUtc;
        ClosedByUserId = closedBy;
    }

    /// <summary>
    /// The AC chair acknowledges a closed item → <c>acknowledged_closed</c> (terminal). Raises
    /// <see cref="AcActionItemClosureAcknowledgedEvent"/>.
    /// </summary>
    public void AcknowledgeClosure(Guid acknowledgedBy, DateTimeOffset nowUtc)
    {
        Transition(AcActionItemStatus.AcknowledgedClosed, AcActionItemStatus.Closed);
        AcknowledgedAt = nowUtc;
        AcknowledgedByUserId = acknowledgedBy;
        RaiseDomainEvent(new AcActionItemClosureAcknowledgedEvent(Id, acknowledgedBy));
    }

    void ISoftDeletable.SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        DeletedBy = deletedBy;
    }

    private void Transition(AcActionItemStatus to, params AcActionItemStatus[] from)
    {
        if (from.Length > 0 && !from.Contains(Status))
        {
            throw new InvalidStateTransitionException("ac_action_item.invalid_transition", $"Cannot move to {to} from {Status}.");
        }

        Status = to;
    }
}
