using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>Immutable snapshot of a response's mutable state, used for the audit-trail before/after.</summary>
public sealed record ResponseState(
    ResponseVerdict? Verdict, string? Comment, string? ValueJson, bool IsDraft, int Version,
    decimal? Score = null, string? Observation = null, string? Recommendation = null);

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

    /// <summary>Auditor's observation — what was seen/found. Free text, optional; captured alongside the verdict.</summary>
    public string? Observation { get; private set; }

    /// <summary>Auditor's recommendation — the suggested corrective action. Free text, optional.</summary>
    public string? Recommendation { get; private set; }

    /// <summary>Type-specific captured value JSON for value response types (text/number/date/rating/choice). Opaque to the domain.</summary>
    public string? ValueJson { get; private set; }

    public Guid ResponderUserId { get; private set; }

    public bool IsDraft { get; private set; }

    /// <summary>Monotonic edit counter surfaced in history and the 409 body; NOT the concurrency token.</summary>
    public int ResponseVersion { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    /// <summary>
    /// A 0-100 score derived from this response once finalised (post-response scoring). Pass/Fail/N-A verdicts
    /// score via a fixed mapping; a Rating value scores via its configured <c>RatingScale</c> point. Value types
    /// with no inherent right answer (text/numeric/date/multiple-choice) are never scored. Computed by the
    /// application layer (it alone can resolve a RatingScale) and applied via <see cref="SetScore"/>; null while
    /// a draft or before the item type/config yields a mapped score.
    /// </summary>
    public decimal? Score { get; private set; }

    internal ChecklistResponse(Guid auditId, Guid checklistItemId, Guid responderUserId)
    {
        AuditId = auditId;
        ChecklistItemId = checklistItemId;
        ResponderUserId = responderUserId;
        ResponseVersion = 0;
    }

    public ResponseState ToState() => new(Verdict, Comment, ValueJson, IsDraft, ResponseVersion, Score, Observation, Recommendation);

    /// <summary>
    /// Create-or-update the response (BR-M5-001/002). Verdict types require a verdict on finalise; value types
    /// (<paramref name="isValueType"/>) require a captured value instead, with the verdict optional so the item
    /// can still be marked Fail to drive an exception. A Fail/N-A verdict always requires a comment.
    /// </summary>
    internal void Apply(ResponseVerdict? verdict, string? comment, string? valueJson, bool isDraft, Guid actorUserId, bool requireCommentOnPass, bool isValueType, DateTimeOffset nowUtc, string? observation = null, string? recommendation = null)
    {
        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        var value = string.IsNullOrWhiteSpace(valueJson) ? null : valueJson.Trim();

        if (!isDraft)
        {
            if (isValueType)
            {
                if (value is null)
                {
                    throw new DomainException("response.value_required", "A value is required for a final response.");
                }
            }
            else if (verdict is null)
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
        Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
        Recommendation = string.IsNullOrWhiteSpace(recommendation) ? null : recommendation.Trim();
        ValueJson = value;
        IsDraft = isDraft;
        ResponderUserId = actorUserId;
        RespondedAt = nowUtc;
        ResponseVersion++;
        // A material edit invalidates any previously computed score; the caller recomputes and re-applies it.
        Score = null;
    }

    /// <summary>Apply the post-response score computed by the application layer (see <see cref="Score"/>).</summary>
    internal void SetScore(decimal? score) => Score = score;
}
