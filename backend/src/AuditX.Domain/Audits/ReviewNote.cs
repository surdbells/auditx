using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits;

/// <summary>
/// A reviewer's note on the audit's working papers — a question or a change the reviewer needs before they will sign
/// the work off. Raised on one checklist item (or on the audit as a whole), answered by the team, and cleared by a
/// reviewer once satisfied. While a note is not cleared, its item cannot be signed off and the audit cannot complete.
/// Child of the <see cref="Audit"/> aggregate; notes are evidence of the review, so they are never deleted.
/// </summary>
public sealed class ReviewNote : Entity, IBelongsToAggregate
{
    public const int MaxLength = 4000;

    private ReviewNote()
    {
    }

    internal ReviewNote(Guid auditId, Guid? checklistItemId, string body, Guid raisedBy, DateTimeOffset raisedAt)
    {
        AuditId = auditId;
        ChecklistItemId = checklistItemId;
        Body = Text(body, "audit.review_note_required", "Say what needs attention.");
        RaisedBy = raisedBy;
        RaisedAt = raisedAt;
        Status = ReviewNoteStatus.Open;
    }

    public Guid AuditId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => AuditId;

    /// <summary>The checklist item the note is about; null for a note on the audit as a whole.</summary>
    public Guid? ChecklistItemId { get; private set; }

    public string Body { get; private set; } = null!;

    public Guid RaisedBy { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public ReviewNoteStatus Status { get; private set; }

    /// <summary>The team's answer: what they changed, or why no change is needed.</summary>
    public string? Answer { get; private set; }

    public Guid? AnsweredBy { get; private set; }

    public DateTimeOffset? AnsweredAt { get; private set; }

    public Guid? ClearedBy { get; private set; }

    public DateTimeOffset? ClearedAt { get; private set; }

    /// <summary>True while the note still needs attention (open, or answered but not yet cleared by a reviewer).</summary>
    public bool IsOutstanding => Status != ReviewNoteStatus.Cleared;

    internal void AnswerWith(string answer, Guid answeredBy, DateTimeOffset nowUtc)
    {
        if (Status != ReviewNoteStatus.Open)
        {
            throw new InvalidStateTransitionException("audit.review_note_not_open", "Only an open review note can be answered.");
        }

        Answer = Text(answer, "audit.review_answer_required", "Say what was changed, or why no change is needed.");
        AnsweredBy = answeredBy;
        AnsweredAt = nowUtc;
        Status = ReviewNoteStatus.Answered;
    }

    internal void Clear(Guid clearedBy, DateTimeOffset nowUtc)
    {
        if (Status == ReviewNoteStatus.Cleared)
        {
            throw new InvalidStateTransitionException("audit.review_note_cleared", "This review note is already cleared.");
        }

        ClearedBy = clearedBy;
        ClearedAt = nowUtc;
        Status = ReviewNoteStatus.Cleared;
    }

    /// <summary>The reviewer is not satisfied with the answer: the note is open again.</summary>
    internal void Reopen()
    {
        if (Status != ReviewNoteStatus.Answered)
        {
            throw new InvalidStateTransitionException("audit.review_note_not_answered", "Only an answered review note can be reopened.");
        }

        Status = ReviewNoteStatus.Open;
    }

    private static string Text(string? value, string code, string message)
    {
        var trimmed = Guard.NotNullOrWhiteSpace(value, code, message).Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainException("audit.review_note_too_long", $"A review note can be at most {MaxLength} characters.");
        }

        return trimmed;
    }
}
