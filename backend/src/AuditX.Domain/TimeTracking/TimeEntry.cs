using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.TimeTracking;

/// <summary>
/// Actual time/effort a user logged against an audit (P0-B). Optionally attributed to a specific
/// checklist item. The sum of an audit's entries is the "actual" side of budget-vs-actual (the budget
/// baseline is <c>Audit.BudgetedHours</c>); grouped by user it drives utilisation reporting.
///
/// Soft-deletable so a correction never loses the historical record; the append-only audit trail
/// separately captures every log/amend/delete. Rowversion-guarded for optimistic concurrency.
/// </summary>
public sealed class TimeEntry : Entity, ISoftDeletable
{
    /// <summary>Upper bound for a single day's entry — a sanity guard, not a policy limit.</summary>
    public const decimal MaxHoursPerEntry = 24m;

    private TimeEntry()
    {
    }

    public Guid AuditId { get; private set; }

    /// <summary>The user who performed (and owns) the work.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Optional link to the specific checklist item the work related to.</summary>
    public Guid? ChecklistItemId { get; private set; }

    /// <summary>The calendar day the work was performed.</summary>
    public DateOnly WorkDate { get; private set; }

    public decimal Hours { get; private set; }

    public TimeEntryCategory Category { get; private set; }

    public string? Notes { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static TimeEntry Log(
        Guid auditId, Guid userId, DateOnly workDate, decimal hours, TimeEntryCategory category,
        Guid? checklistItemId, string? notes)
    {
        Guard.Against(auditId == Guid.Empty, "time_entry.audit_required", "An audit is required.");
        Guard.Against(userId == Guid.Empty, "time_entry.user_required", "A user is required.");
        ValidateHours(hours);

        return new TimeEntry
        {
            AuditId = auditId,
            UserId = userId,
            WorkDate = workDate,
            Hours = hours,
            Category = category,
            ChecklistItemId = checklistItemId,
            Notes = Normalise(notes),
        };
    }

    /// <summary>Correct an entry's day / hours / category / linkage / notes. The owner is immutable.</summary>
    public void Amend(DateOnly workDate, decimal hours, TimeEntryCategory category, Guid? checklistItemId, string? notes)
    {
        EnsureNotDeleted();
        ValidateHours(hours);
        WorkDate = workDate;
        Hours = hours;
        Category = category;
        ChecklistItemId = checklistItemId;
        Notes = Normalise(notes);
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new DomainException("time_entry.deleted", "This time entry has been deleted and cannot be amended.");
        }
    }

    private static void ValidateHours(decimal hours)
    {
        Guard.Against(hours <= 0, "time_entry.hours_positive", "Hours must be greater than zero.");
        Guard.Against(hours > MaxHoursPerEntry, "time_entry.hours_max", "A single time entry cannot exceed 24 hours.");
    }

    private static string? Normalise(string? notes)
        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
