using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Exceptions;

/// <summary>
/// A request to move a due date: the whole finding's target date (<see cref="ActionId"/> null) or one remediation
/// action's. Raised by the people who must remediate; decided by the audit side, never by the person who asked.
/// Child of the <see cref="AuditException"/> aggregate, kept after the decision as the extension history.
/// </summary>
public sealed class DueDateExtensionRequest : Entity, IBelongsToAggregate
{
    private DueDateExtensionRequest()
    {
    }

    public Guid ExceptionId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => ExceptionId;

    /// <summary>The remediation action whose date moves, or null for the finding's own target date.</summary>
    public Guid? ActionId { get; private set; }

    public DateOnly CurrentDate { get; private set; }

    public DateOnly RequestedDate { get; private set; }

    public string Reason { get; private set; } = null!;

    public Guid RequestedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public ExtensionRequestStatus Status { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecisionNote { get; private set; }

    internal DueDateExtensionRequest(
        Guid exceptionId, Guid? actionId, DateOnly currentDate, DateOnly requestedDate, string reason, Guid requestedBy, DateTimeOffset requestedAt)
    {
        ExceptionId = exceptionId;
        ActionId = actionId;
        CurrentDate = currentDate;
        RequestedDate = requestedDate;
        Reason = reason;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
        Status = ExtensionRequestStatus.Pending;
    }

    internal void Decide(ExtensionRequestStatus outcome, Guid decidedBy, string? note, DateTimeOffset decidedAt)
    {
        Status = outcome;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
