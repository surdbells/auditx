using AuditX.Domain.Ac.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Ac;

/// <summary>
/// A generic Audit-Committee comment (M13, A3/BR-M13-010). Polymorphic: attached to a plan, a pack or a finding via
/// (<see cref="TargetType"/>, <see cref="TargetId"/>). Append-only — there is no edit/delete verb.
/// </summary>
public sealed class AcComment : AggregateRoot
{
    private AcComment()
    {
    }

    public AcCommentTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public string CommentText { get; private set; } = null!;

    public Guid AuthorUserId { get; private set; }

    public DateTimeOffset CommentedAt { get; private set; }

    /// <summary>Create a comment against a polymorphic target. Raises <see cref="AcCommentAddedEvent"/> (→ CIA via M10).</summary>
    public static AcComment Create(AcCommentTargetType targetType, Guid targetId, string commentText, Guid authorUserId, DateTimeOffset nowUtc)
    {
        if (targetId == Guid.Empty)
        {
            throw new DomainException("ac_comment.target_required", "A comment target id is required.");
        }

        var comment = new AcComment
        {
            TargetType = targetType,
            TargetId = targetId,
            CommentText = Guard.NotNullOrWhiteSpace(commentText, "ac_comment.text_required", "Comment text is required.").Trim(),
            AuthorUserId = authorUserId,
            CommentedAt = nowUtc,
        };
        comment.RaiseDomainEvent(new AcCommentAddedEvent(comment.Id, targetType.ToString().ToLowerInvariant(), targetId, authorUserId));
        return comment;
    }
}
