using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>Immutable snapshot of a response's mutable state, used for the audit-trail before/after.</summary>
public sealed record ResponseState(ResponseVerdict? Verdict, string? Comment, bool IsDraft, int Version);

/// <summary>Result of recording a response: the entity, its before/after snapshots, and whether the audit auto-transitioned to Under Review.</summary>
public sealed record ResponseMutation(ChecklistResponse Response, ResponseState? Before, ResponseState After, bool AutoTransitioned);

/// <summary>
/// An auditor's response to a single checklist item (M5). One row per item (create-or-update). A child
/// of the <see cref="Audit"/> aggregate (via <see cref="IBelongsToAggregate"/>) so its mutations advance
/// the audit's rowversion and ride the same optimistic-concurrency check. A null <see cref="Verdict"/>
/// with <see cref="IsDraft"/> = true is an author-private draft.
/// </summary>
public sealed class ChecklistResponse : Entity, IBelongsToAggregate
{
    private ChecklistResponse()
    {
    }

    public Guid AuditId { get; private set; }

    public Guid ChecklistItemId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AuditId;

    public ResponseVerdict? Verdict { get; private set; }

    public string? Comment { get; private set; }

    public Guid ResponderUserId { get; private set; }

    public bool IsDraft { get; private set; }

    /// <summary>Monotonic edit counter surfaced in history and the 409 body; NOT the concurrency token.</summary>
    public int ResponseVersion { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    internal ChecklistResponse(Guid auditId, Guid checklistItemId, Guid responderUserId)
    {
        AuditId = auditId;
        ChecklistItemId = checklistItemId;
        ResponderUserId = responderUserId;
        ResponseVersion = 0;
    }

    public ResponseState ToState() => new(Verdict, Comment, IsDraft, ResponseVersion);

    /// <summary>Create-or-update the response. Enforces BR-M5-001/002 (final responses need a verdict; Fail/N/A need a comment).</summary>
    internal void Apply(ResponseVerdict? verdict, string? comment, bool isDraft, Guid actorUserId, bool requireCommentOnPass, DateTimeOffset nowUtc)
    {
        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        if (!isDraft)
        {
            if (verdict is null)
            {
                throw new DomainException("response.verdict_required", "A final response must have a verdict.");
            }

            if (verdict is ResponseVerdict.Fail && trimmed is null)
            {
                throw new DomainException("response.comment_required", "A comment is required for a Fail verdict.");
            }

            if (verdict is ResponseVerdict.Na && trimmed is null)
            {
                throw new DomainException("response.comment_required", "A comment is required for an N/A verdict.");
            }

            if (verdict is ResponseVerdict.Pass && requireCommentOnPass && trimmed is null)
            {
                throw new DomainException("response.comment_required_pass", "A comment is required for a Pass verdict under bank policy.");
            }
        }

        Verdict = verdict;
        Comment = trimmed;
        IsDraft = isDraft;
        ResponderUserId = actorUserId;
        RespondedAt = nowUtc;
        ResponseVersion++;
    }
}
