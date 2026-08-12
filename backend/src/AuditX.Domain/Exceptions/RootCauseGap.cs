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
    private readonly List<RootCauseGapRemediation> _remediations = [];

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

    public IReadOnlyList<RootCauseGapRemediation> Remediations => _remediations.AsReadOnly();

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

    // ---- Remediation plan (the concrete corrective actions that close the systemic gap) ----

    public RootCauseGapRemediation AddRemediation(string description, Guid ownerUserId, DateOnly? dueDate, Guid createdByUserId, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        var item = RootCauseGapRemediation.Create(Id, description, ownerUserId, dueDate, createdByUserId, nowUtc);
        _remediations.Add(item);
        return item;
    }

    public void UpdateRemediation(Guid remediationId, string description, Guid ownerUserId, DateOnly? dueDate)
    {
        EnsureOpen();
        FindRemediation(remediationId).UpdateDetails(description, ownerUserId, dueDate);
    }

    public void CompleteRemediation(Guid remediationId, string? note, Guid completedByUserId, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        FindRemediation(remediationId).Complete(note, completedByUserId, nowUtc);
    }

    public void ReopenRemediation(Guid remediationId)
    {
        EnsureOpen();
        FindRemediation(remediationId).Reopen();
    }

    public void RemoveRemediation(Guid remediationId)
    {
        EnsureOpen();
        _remediations.Remove(FindRemediation(remediationId));
    }

    private RootCauseGapRemediation FindRemediation(Guid remediationId)
        => _remediations.FirstOrDefault(r => r.Id == remediationId)
            ?? throw new DomainException("root_cause_gap.remediation_not_found", "That remediation action is not part of this gap.");

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

/// <summary>
/// One corrective action in a root-cause gap's remediation plan — the plan-like structure that drives the gap
/// to closure. Each action has its own owner and (optional) due date and moves Open → Completed independently.
/// A child of the gap aggregate (via <see cref="IBelongsToAggregate"/>) so its mutations advance the gap's
/// rowversion and ride the same optimistic-concurrency check.
/// </summary>
public sealed class RootCauseGapRemediation : Entity, IBelongsToAggregate
{
    private RootCauseGapRemediation()
    {
    }

    public Guid RootCauseGapId { get; private set; }

    Guid IBelongsToAggregate.AggregateRootId => RootCauseGapId;

    public string Description { get; private set; } = null!;

    public Guid OwnerUserId { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public RootCauseGapRemediationStatus Status { get; private set; }

    public string? CompletionNote { get; private set; }

    public Guid? CompletedByUserId { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    internal static RootCauseGapRemediation Create(Guid rootCauseGapId, string description, Guid ownerUserId, DateOnly? dueDate, Guid createdByUserId, DateTimeOffset nowUtc)
    {
        Guard.Against(ownerUserId == Guid.Empty, "root_cause_gap_remediation.owner_required", "A remediation owner is required.");
        return new RootCauseGapRemediation
        {
            RootCauseGapId = rootCauseGapId,
            Description = Guard.NotNullOrWhiteSpace(description, "root_cause_gap_remediation.description_required", "A remediation description is required.").Trim(),
            OwnerUserId = ownerUserId,
            DueDate = dueDate,
            Status = RootCauseGapRemediationStatus.Open,
            // CreatedAt / CreatedBy come from the Entity base; the auditing interceptor stamps them on insert, but
            // set CreatedAt now too so the just-added action sorts correctly in the DTO returned this request.
            CreatedAt = nowUtc,
            CreatedBy = createdByUserId,
        };
    }

    internal void UpdateDetails(string description, Guid ownerUserId, DateOnly? dueDate)
    {
        Guard.Against(ownerUserId == Guid.Empty, "root_cause_gap_remediation.owner_required", "A remediation owner is required.");
        Description = Guard.NotNullOrWhiteSpace(description, "root_cause_gap_remediation.description_required", "A remediation description is required.").Trim();
        OwnerUserId = ownerUserId;
        DueDate = dueDate;
    }

    internal void Complete(string? note, Guid completedByUserId, DateTimeOffset nowUtc)
    {
        if (Status == RootCauseGapRemediationStatus.Completed)
        {
            throw new InvalidStateTransitionException("root_cause_gap_remediation.already_completed", "This remediation action is already completed.");
        }

        Status = RootCauseGapRemediationStatus.Completed;
        CompletionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CompletedByUserId = completedByUserId;
        CompletedAt = nowUtc;
    }

    internal void Reopen()
    {
        if (Status != RootCauseGapRemediationStatus.Completed)
        {
            throw new InvalidStateTransitionException("root_cause_gap_remediation.not_completed", "Only a completed remediation action can be reopened.");
        }

        Status = RootCauseGapRemediationStatus.Open;
        CompletionNote = null;
        CompletedByUserId = null;
        CompletedAt = null;
    }
}
