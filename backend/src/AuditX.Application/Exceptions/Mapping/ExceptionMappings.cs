using AuditX.Application.Common.Enums;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Exceptions.Mapping;

public static class ExceptionMappings
{
    public static bool IsOverdue(this AuditException e, DateOnly today)
        => e.Status is not (ExceptionStatus.Closed or ExceptionStatus.Cancelled) && e.TargetDate < today;

    private static int DaysPastTarget(this AuditException e, DateOnly today)
        => e.IsOverdue(today) ? today.DayNumber - e.TargetDate.DayNumber : 0;

    /// <summary>Status surfaces a derived <c>pending_cia_approval</c> label while a Critical close awaits countersign.</summary>
    private static string StatusLabel(this AuditException e)
        => e.Status == ExceptionStatus.PendingClosure && e.CiaPending ? "pending_cia_approval" : e.Status.ToSnake();

    /// <summary>
    /// Management-response timeliness against the response due date: <c>on_time</c> / <c>late</c> once responded,
    /// <c>overdue</c> / <c>due</c> while still awaited. Null when no due date is set (or the finding is cancelled).
    /// </summary>
    private static string? ResponseTimeliness(this AuditException e, DateOnly today)
    {
        if (e.ManagementResponseDueDate is not { } due || e.Status == ExceptionStatus.Cancelled)
        {
            return null;
        }

        if (e.ManagementRespondedAt is { } at)
        {
            return DateOnly.FromDateTime(at.UtcDateTime) <= due ? "on_time" : "late";
        }

        return due < today ? "overdue" : "due";
    }

    public static ExceptionDto ToDto(this AuditException e, DateOnly today) => new(
        e.Id, e.AuditId, e.ChecklistItemId, e.AuditableEntityId, e.Title, e.Severity.ToSnake(), e.StatusLabel(),
        e.RootCause, e.Recommendation, e.Category, e.RootCauseCategory, e.OwnerUserId, e.RaisedByUserId, e.RaisedAt, e.TargetDate,
        e.FinancialImpact, e.FinancialImpactCurrency,
        e.TargetDateOverridden, e.IsRecurrence, e.RecurrenceOfExceptionId, e.CiaPending,
        e.IsOverdue(today), e.DaysPastTarget(today), e.MapSubmittedAt, e.MapApprovedAt, e.MapRejectionReason,
        e.ClosureEvidenceNote, e.ClosedBy, e.ClosedAt, e.CiaCountersignedBy, e.CancellationReason,
        e.ManagementResponseDecision?.ToSnake(), e.ManagementResponseComment, e.ManagementRespondedBy, e.ManagementRespondedAt,
        e.ManagementResponseDueDate, e.ResponseTimeliness(today),
        e.ReopenCount, e.ReopenedBy, e.ReopenedAt, e.ReopenReason,
        RowVersionToken.Encode(e.Version),
        e.MapActions.Select(a => a.ToDto()).ToArray(),
        e.Verifications.OrderByDescending(v => v.VerifiedAt).Select(v => v.ToDto()).ToArray());

    public static MapActionDto ToDto(this MapAction a) => new(
        a.Id, a.ExceptionId, a.Description, a.OwnerUserId, a.TargetDate, a.ExpectedEvidenceType,
        a.Status.ToSnake(), a.CompletedAt, a.CompletedBy);

    public static FindingVerificationDto ToDto(this FindingVerification v) => new(
        v.Id, v.ExceptionId, v.Result.ToSnake(), v.VerifiedByUserId, v.VerifiedAt, v.Notes);

    public static ExceptionListItemDto ToListItemDto(this AuditException e, DateOnly today) => new(
        e.Id, e.AuditId, e.Title, e.Severity.ToSnake(), e.StatusLabel(), e.OwnerUserId, e.TargetDate,
        e.IsOverdue(today), e.DaysPastTarget(today), e.IsRecurrence, e.RaisedAt);
}
