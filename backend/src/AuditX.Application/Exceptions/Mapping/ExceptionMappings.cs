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

    public static ExceptionDto ToDto(this AuditException e, DateOnly today) => new(
        e.Id, e.AuditId, e.ChecklistItemId, e.AuditableEntityId, e.Title, e.Severity.ToSnake(), e.StatusLabel(),
        e.RootCause, e.Recommendation, e.Category, e.OwnerUserId, e.RaisedByUserId, e.RaisedAt, e.TargetDate,
        e.TargetDateOverridden, e.IsRecurrence, e.RecurrenceOfExceptionId, e.CiaPending,
        e.IsOverdue(today), e.DaysPastTarget(today), e.MapSubmittedAt, e.MapApprovedAt, e.MapRejectionReason,
        e.ClosureEvidenceNote, e.ClosedBy, e.ClosedAt, e.CiaCountersignedBy, e.CancellationReason,
        Convert.ToBase64String(e.Version ?? []),
        e.MapActions.Select(a => a.ToDto()).ToArray());

    public static MapActionDto ToDto(this MapAction a) => new(
        a.Id, a.ExceptionId, a.Description, a.OwnerUserId, a.TargetDate, a.ExpectedEvidenceType,
        a.Status.ToSnake(), a.CompletedAt, a.CompletedBy);

    public static ExceptionListItemDto ToListItemDto(this AuditException e, DateOnly today) => new(
        e.Id, e.AuditId, e.Title, e.Severity.ToSnake(), e.StatusLabel(), e.OwnerUserId, e.TargetDate,
        e.IsOverdue(today), e.DaysPastTarget(today), e.IsRecurrence);
}
