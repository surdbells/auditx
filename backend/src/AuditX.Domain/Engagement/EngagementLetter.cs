using AuditX.Domain.Common;
using AuditX.Domain.Engagement.Events;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Engagement;

/// <summary>
/// The engagement letter: what the audit team tells the area it is about to audit — objectives, scope, timing, who
/// is involved and what is expected of management. The team drafts it and issues it to the auditee, who acknowledges
/// it. Changing a letter once issued makes it a draft of the next revision, to be issued and acknowledged again; the
/// revision it replaces is kept, with who issued and acknowledged it, as evidence of what was agreed.
/// One letter per audit; a standalone aggregate (like the audit's reports) so it is versioned on its own.
/// </summary>
public sealed class EngagementLetter : AggregateRoot
{
    public const int MaxBodyLength = 20000;
    public const int MaxCommentLength = 2000;

    private readonly List<EngagementLetterRevision> _previousRevisions = [];

    private EngagementLetter()
    {
    }

    public Guid AuditId { get; private set; }

    public string Body { get; private set; } = null!;

    public EngagementLetterStatus Status { get; private set; }

    /// <summary>1 for the first letter; each change after issue starts the next revision.</summary>
    public int Revision { get; private set; }

    /// <summary>Who last wrote the text of this revision.</summary>
    public Guid DraftedBy { get; private set; }

    public DateTimeOffset DraftedAt { get; private set; }

    public Guid? IssuedBy { get; private set; }

    public DateTimeOffset? IssuedAt { get; private set; }

    public Guid? AcknowledgedBy { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    /// <summary>What the auditee added when acknowledging (a correction to the contacts, a constraint on timing).</summary>
    public string? AcknowledgementComment { get; private set; }

    public byte[] Version { get; private set; } = [];

    /// <summary>The revisions this one replaced, oldest first.</summary>
    public IReadOnlyList<EngagementLetterRevision> PreviousRevisions => _previousRevisions.AsReadOnly();

    public static EngagementLetter Draft(Guid auditId, string body, Guid draftedBy, DateTimeOffset nowUtc)
    {
        Guard.Against(auditId == Guid.Empty, "engagement_letter.audit_required", "An audit is required.");
        return new EngagementLetter
        {
            AuditId = auditId,
            Body = CleanBody(body),
            Status = EngagementLetterStatus.Draft,
            Revision = 1,
            DraftedBy = draftedBy,
            DraftedAt = nowUtc,
        };
    }

    /// <summary>
    /// Change the letter's text. A draft is edited in place; an issued (or acknowledged) letter is kept as a previous
    /// revision and the new text becomes a draft of the next one, to be issued and acknowledged again.
    /// </summary>
    public void Revise(string body, Guid draftedBy, DateTimeOffset nowUtc)
    {
        var cleaned = CleanBody(body);
        if (Status != EngagementLetterStatus.Draft)
        {
            if (string.Equals(cleaned, Body, StringComparison.Ordinal))
            {
                throw new DomainException("engagement_letter.unchanged", "The letter is unchanged, so there is nothing to issue again.");
            }

            _previousRevisions.Add(new EngagementLetterRevision(
                Revision, Body, IssuedBy, IssuedAt, AcknowledgedBy, AcknowledgedAt, AcknowledgementComment));
            Revision++;
            IssuedBy = null;
            IssuedAt = null;
            AcknowledgedBy = null;
            AcknowledgedAt = null;
            AcknowledgementComment = null;
            Status = EngagementLetterStatus.Draft;
        }

        Body = cleaned;
        DraftedBy = draftedBy;
        DraftedAt = nowUtc;
    }

    /// <summary>Send the draft to the auditee, who is told and asked to acknowledge it.</summary>
    public void Issue(Guid issuedBy, DateTimeOffset nowUtc, string auditName, Guid auditeeUserId)
    {
        if (Status != EngagementLetterStatus.Draft)
        {
            throw new InvalidStateTransitionException("engagement_letter.not_draft", "This revision of the letter has already been issued.");
        }

        IssuedBy = issuedBy;
        IssuedAt = nowUtc;
        Status = EngagementLetterStatus.Issued;
        RaiseDomainEvent(new EngagementLetterIssuedEvent(Id, AuditId, auditName, Revision, auditeeUserId, issuedBy));
    }

    /// <summary>The auditee confirms they have received the letter and understand what it asks of them.</summary>
    public void Acknowledge(Guid acknowledgedBy, string? comment, DateTimeOffset nowUtc, string auditName)
    {
        if (Status != EngagementLetterStatus.Issued)
        {
            throw new InvalidStateTransitionException("engagement_letter.not_issued", "Only an issued letter that is awaiting acknowledgement can be acknowledged.");
        }

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmed is { Length: > MaxCommentLength })
        {
            throw new DomainException("engagement_letter.comment_too_long", $"Keep the comment to {MaxCommentLength} characters or fewer.");
        }

        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = nowUtc;
        AcknowledgementComment = trimmed;
        Status = EngagementLetterStatus.Acknowledged;
        RaiseDomainEvent(new EngagementLetterAcknowledgedEvent(Id, AuditId, auditName, Revision, acknowledgedBy, IssuedBy!.Value));
    }

    private static string CleanBody(string? body)
    {
        var trimmed = Guard.NotNullOrWhiteSpace(body, "engagement_letter.body_required", "Write the letter before saving it.").Trim();
        if (trimmed.Length > MaxBodyLength)
        {
            throw new DomainException("engagement_letter.body_too_long", $"Keep the letter to {MaxBodyLength} characters or fewer.");
        }

        return trimmed;
    }
}

/// <summary>A revision of an engagement letter that a later one replaced: its text and who issued and acknowledged it.</summary>
public sealed class EngagementLetterRevision : Entity
{
    private EngagementLetterRevision()
    {
    }

    internal EngagementLetterRevision(
        int revision, string body, Guid? issuedBy, DateTimeOffset? issuedAt, Guid? acknowledgedBy, DateTimeOffset? acknowledgedAt, string? acknowledgementComment)
    {
        Revision = revision;
        Body = body;
        IssuedBy = issuedBy;
        IssuedAt = issuedAt;
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = acknowledgedAt;
        AcknowledgementComment = acknowledgementComment;
    }

    public int Revision { get; private set; }

    public string Body { get; private set; } = null!;

    public Guid? IssuedBy { get; private set; }

    public DateTimeOffset? IssuedAt { get; private set; }

    public Guid? AcknowledgedBy { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public string? AcknowledgementComment { get; private set; }
}
