using AuditX.Domain.Common;

namespace AuditX.Domain.Reports;

/// <summary>
/// A comment on a DRAFT engagement report — management's (or the audit team's) remarks on the draft before the final
/// report is issued. Child of the <see cref="Report"/> aggregate. Comments are a record of the discussion, so they are
/// never edited or removed.
/// </summary>
public sealed class ReportComment : Entity, IBelongsToAggregate
{
    public const int MaxBodyLength = 4000;

    private ReportComment()
    {
    }

    internal ReportComment(Guid reportId, int reportVersionNumber, Guid authorUserId, string body, DateTimeOffset postedAt)
    {
        ReportId = reportId;
        ReportVersionNumber = reportVersionNumber;
        AuthorUserId = authorUserId;
        var trimmed = Guard.NotNullOrWhiteSpace(body, "report.comment_required", "A comment cannot be empty.").Trim();
        if (trimmed.Length > MaxBodyLength)
        {
            throw new DomainException("report.comment_too_long", $"A comment can be at most {MaxBodyLength} characters.");
        }

        Body = trimmed;
        PostedAt = postedAt;
    }

    public Guid ReportId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => ReportId;

    /// <summary>The report version the comment was made on.</summary>
    public int ReportVersionNumber { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = null!;

    public DateTimeOffset PostedAt { get; private set; }
}
