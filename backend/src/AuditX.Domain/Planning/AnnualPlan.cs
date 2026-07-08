using System.Text.Json;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Planning.Events;

namespace AuditX.Domain.Planning;

/// <summary>Immutable record of the Audit Committee's decision on a plan, including any commentary.</summary>
public sealed record ApprovalDecisionRecord(
    string Decision,
    string? Detail,
    IReadOnlyList<string> Comments,
    Guid DecidedBy,
    DateTimeOffset DecidedAt);

/// <summary>
/// The annual, risk-based audit plan (M3). Aggregate root over its plan items. Drives a state machine
/// from Draft through Audit Committee approval, with minor/material revision paths and year-end closure.
/// </summary>
public sealed class AnnualPlan : AggregateRoot
{
    private readonly List<PlanItem> _items = [];

    private AnnualPlan()
    {
    }

    public string PeriodLabel { get; private set; } = null!;

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public PlanStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? ApprovalDecisionJson { get; private set; }

    public IReadOnlyList<PlanItem> Items => _items.AsReadOnly();

    public ApprovalDecisionRecord? ApprovalDecision => ApprovalDecisionJson is null
        ? null
        : JsonSerializer.Deserialize<ApprovalDecisionRecord>(ApprovalDecisionJson);

    public static AnnualPlan Create(string periodLabel, DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodEnd <= periodStart)
        {
            throw new DomainException("plan.period_invalid", "Plan period end must be after its start.");
        }

        return new AnnualPlan
        {
            PeriodLabel = Guard.NotNullOrWhiteSpace(periodLabel, "plan.period_label_required", "Period label is required."),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = PlanStatus.Draft,
        };
    }

    public void UpdatePeriod(string periodLabel, DateOnly periodStart, DateOnly periodEnd)
    {
        EnsureStatus("plan.not_editable", PlanStatus.Draft);
        if (periodEnd <= periodStart)
        {
            throw new DomainException("plan.period_invalid", "Plan period end must be after its start.");
        }

        PeriodLabel = Guard.NotNullOrWhiteSpace(periodLabel, "plan.period_label_required", "Period label is required.");
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    public PlanItem AddItem(Guid entityId, string auditType, DateOnly start, DateOnly end, decimal? effortDays, Guid? assignedLeadUserId)
    {
        EnsureStatus("plan.not_editable", PlanStatus.Draft, PlanStatus.RevisionsRequested);
        EnsureWithinPeriod(start, end);
        var item = new PlanItem(Id, entityId, auditType, start, end, effortDays, assignedLeadUserId, _items.Count);
        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureStatus("plan.not_editable", PlanStatus.Draft, PlanStatus.RevisionsRequested);
        _items.Remove(FindItem(itemId));
        RenumberItems();
    }

    public void ReorderItems(IReadOnlyList<Guid> orderedItemIds)
    {
        EnsureStatus("plan.not_editable", PlanStatus.Draft, PlanStatus.RevisionsRequested);
        if (orderedItemIds.Count != _items.Count || orderedItemIds.Distinct().Count() != _items.Count)
        {
            throw new DomainException("plan.reorder_mismatch", "The reorder must list every plan item exactly once.");
        }

        for (var index = 0; index < orderedItemIds.Count; index++)
        {
            FindItem(orderedItemIds[index]).SetOrder(index);
        }
    }

    private void RenumberItems()
    {
        var ordered = _items.OrderBy(i => i.OrderIndex).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SetOrder(index);
        }
    }

    public void Submit(DateTimeOffset nowUtc)
    {
        EnsureStatus("plan.not_submittable", PlanStatus.Draft, PlanStatus.RevisionsRequested);
        if (_items.Count == 0)
        {
            throw new DomainException("plan.no_items", "A plan must have at least one item before submission.");
        }

        Status = PlanStatus.Submitted;
        SubmittedAt = nowUtc;
        RaiseDomainEvent(new PlanSubmittedEvent(Id, PeriodLabel));
    }

    public void RecordDecision(AcDecisionOutcome outcome, string? detail, IReadOnlyList<string> comments, Guid decidedBy, DateTimeOffset nowUtc)
    {
        EnsureStatus("plan.not_decidable", PlanStatus.Submitted, PlanStatus.RevisionSubmitted);

        ApprovalDecisionJson = JsonSerializer.Serialize(new ApprovalDecisionRecord(
            outcome.ToString(), detail, comments, decidedBy, nowUtc));

        switch (outcome)
        {
            case AcDecisionOutcome.Approved:
                Status = PlanStatus.Approved;
                ApprovedAt = nowUtc;
                break;
            case AcDecisionOutcome.RevisionsRequested:
                Status = PlanStatus.RevisionsRequested;
                break;
            case AcDecisionOutcome.Rejected:
                // Status preserved; the rejection is captured in the decision record.
                break;
        }

        RaiseDomainEvent(new PlanDecidedEvent(Id, outcome.ToString(), decidedBy));
    }

    /// <summary>Material revision of an approved plan: re-opens for AC re-approval.</summary>
    public void BeginMaterialRevision(DateTimeOffset nowUtc)
    {
        EnsureStatus("plan.not_revisable", PlanStatus.Approved);
        Status = PlanStatus.RevisionSubmitted;
        SubmittedAt = nowUtc;
    }

    /// <summary>Minor revision of an approved plan: adjust a plan item's dates in place, staying Approved.</summary>
    public void ApplyMinorItemDateChange(Guid itemId, DateOnly start, DateOnly end)
    {
        EnsureStatus("plan.not_revisable", PlanStatus.Approved);
        EnsureWithinPeriod(start, end);
        FindItem(itemId).SetDates(start, end);
    }

    public void Close()
    {
        EnsureStatus("plan.not_closable", PlanStatus.Approved);
        Status = PlanStatus.Closed;
        RaiseDomainEvent(new PlanClosedEvent(Id));
    }

    /// <summary>
    /// Link a freshly-created audit to a plan item. By default audits may only be launched from an
    /// <see cref="PlanStatus.Approved"/> plan (the Audit-Committee governance gate). When the deployment
    /// opts in via <c>BankSettings.AllowAuditLaunchBeforeApproval</c>, the caller passes
    /// <paramref name="allowBeforeApproval"/> = true and audits may be launched from any non-closed plan.
    /// </summary>
    public void LinkAuditToItem(Guid itemId, Guid auditId, bool allowBeforeApproval = false)
    {
        var linkable = allowBeforeApproval ? Status != PlanStatus.Closed : Status == PlanStatus.Approved;
        if (!linkable)
        {
            throw new InvalidStateTransitionException(
                "plan.item_not_linkable",
                allowBeforeApproval
                    ? "Audits cannot be launched from a closed plan."
                    : "Audits can only be linked to items of an approved plan.");
        }

        var item = FindItem(itemId);
        item.LinkAudit(auditId);
        RaiseDomainEvent(new PlanItemLinkedToAuditEvent(item.Id, auditId));
    }

    /// <summary>Mark a plan item completed when its linked audit completes (M4 → M3, US-M3-019).</summary>
    public void MarkPlanItemCompleted(Guid itemId) => FindItem(itemId).MarkCompleted();

    /// <summary>Mark a plan item deferred when its linked audit is cancelled/deferred (M4 → M3).</summary>
    public void MarkPlanItemDeferred(Guid itemId) => FindItem(itemId).MarkDeferred();

    private void EnsureWithinPeriod(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainException("plan.item_date_order", "Planned end date must be on or after the planned start date.");
        }

        if (start < PeriodStart || end > PeriodEnd)
        {
            throw new DomainException("plan.item_out_of_period", "Plan item dates must fall within the plan period.");
        }
    }

    private PlanItem FindItem(Guid itemId)
        => _items.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("plan.item_not_found", "Plan item not found.");

    private void EnsureStatus(string code, params PlanStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStateTransitionException(code, $"Operation not permitted while the plan is {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
