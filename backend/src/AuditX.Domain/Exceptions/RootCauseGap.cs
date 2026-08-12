using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Exceptions;

/// <summary>
/// A systemic root-cause gap: an underlying weakness (e.g. "no segregation-of-duties policy") that surfaces
/// across multiple findings. Rather than remediate each finding in isolation, the audit function tracks the
/// shared root cause as a first-class item with its own owner, target date and open→closed lifecycle, and links
/// the contributing exceptions to it. Closing requires a rationale so the systemic remediation is evidenced.
/// </summary>
public sealed class RootCauseGap : AggregateRoot
{
    private readonly List<RootCauseGapExceptionLink> _links = [];

    private RootCauseGap()
    {
    }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Optional structured root-cause taxonomy code (reference data), shared with the exception root-cause categories.</summary>
    public string? Category { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public RootCauseGapStatus Status { get; private set; }

    public Guid IdentifiedByUserId { get; private set; }

    public DateTimeOffset IdentifiedAt { get; private set; }

    public string? ClosureRationale { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<RootCauseGapExceptionLink> Links => _links.AsReadOnly();

    public bool IsClosed => Status == RootCauseGapStatus.Closed;

    public static RootCauseGap Open(
        string title, string? description, string? category, Guid ownerUserId, DateOnly? targetDate, Guid identifiedByUserId, DateTimeOffset nowUtc)
    {
        Guard.Against(ownerUserId == Guid.Empty, "root_cause_gap.owner_required", "An owner is required.");
        return new RootCauseGap
        {
            Title = Guard.NotNullOrWhiteSpace(title, "root_cause_gap.title_required", "A title is required.").Trim(),
            Description = Normalise(description),
            Category = Normalise(category),
            OwnerUserId = ownerUserId,
            TargetDate = targetDate,
            Status = RootCauseGapStatus.Open,
            IdentifiedByUserId = identifiedByUserId,
            IdentifiedAt = nowUtc,
        };
    }

    public void UpdateDetails(string title, string? description, string? category, Guid ownerUserId, DateOnly? targetDate)
    {
        EnsureOpen();
        Guard.Against(ownerUserId == Guid.Empty, "root_cause_gap.owner_required", "An owner is required.");
        Title = Guard.NotNullOrWhiteSpace(title, "root_cause_gap.title_required", "A title is required.").Trim();
        Description = Normalise(description);
        Category = Normalise(category);
        OwnerUserId = ownerUserId;
        TargetDate = targetDate;
    }

    public void Close(string rationale, Guid closedByUserId, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        var trimmed = Guard.NotNullOrWhiteSpace(rationale, "root_cause_gap.rationale_required", "A closure rationale is required.").Trim();
        if (trimmed.Length < 10)
        {
            throw new DomainException("root_cause_gap.rationale_too_short", "The closure rationale must be at least 10 characters.");
        }

        Status = RootCauseGapStatus.Closed;
        ClosureRationale = trimmed;
        ClosedByUserId = closedByUserId;
        ClosedAt = nowUtc;
    }

    public void Reopen()
    {
        if (Status != RootCauseGapStatus.Closed)
        {
            throw new InvalidStateTransitionException("root_cause_gap.not_closed", "Only a closed root-cause gap can be reopened.");
        }

        Status = RootCauseGapStatus.Open;
        ClosureRationale = null;
        ClosedByUserId = null;
        ClosedAt = null;
    }

    public RootCauseGapExceptionLink LinkException(Guid exceptionId, Guid linkedByUserId)
    {
        EnsureOpen();
        var existing = _links.FirstOrDefault(l => l.ExceptionId == exceptionId);
        if (existing is not null)
        {
            return existing;
        }

        var link = RootCauseGapExceptionLink.Create(Id, exceptionId, linkedByUserId);
        _links.Add(link);
        return link;
    }

    public void UnlinkException(Guid exceptionId)
    {
        EnsureOpen();
        var link = _links.FirstOrDefault(l => l.ExceptionId == exceptionId);
        if (link is not null)
        {
            _links.Remove(link);
        }
    }

    private void EnsureOpen()
    {
        if (Status != RootCauseGapStatus.Open)
        {
            throw new InvalidStateTransitionException("root_cause_gap.closed", "This root-cause gap is closed; reopen it to make changes.");
        }
    }

    private static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Links a <see cref="RootCauseGap"/> to a contributing finding (audit exception). A child of the gap aggregate
/// (via <see cref="IBelongsToAggregate"/>) so link/unlink mutations advance the gap's rowversion and ride its
/// optimistic-concurrency check. Create to link, delete to unlink.
/// </summary>
public sealed class RootCauseGapExceptionLink : Entity, IBelongsToAggregate
{
    private RootCauseGapExceptionLink()
    {
    }

    public Guid RootCauseGapId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => RootCauseGapId;

    public Guid ExceptionId { get; private set; }

    public Guid LinkedByUserId { get; private set; }

    internal static RootCauseGapExceptionLink Create(Guid rootCauseGapId, Guid exceptionId, Guid linkedByUserId)
    {
        Guard.Against(exceptionId == Guid.Empty, "root_cause_gap_link.exception_required", "An exception is required.");
        return new RootCauseGapExceptionLink { RootCauseGapId = rootCauseGapId, ExceptionId = exceptionId, LinkedByUserId = linkedByUserId };
    }
}
