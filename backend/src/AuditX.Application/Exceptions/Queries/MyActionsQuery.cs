using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Exceptions.Queries;

/// <summary>
/// The signed-in user's follow-up worklist: everything on findings still being remediated that waits on them. It is
/// scoped to the caller by construction (only findings that name them), so it needs no permission beyond sign-in.
/// </summary>
public sealed record MyActionsQuery : IQuery<IReadOnlyList<MyActionItemDto>>;

public sealed class MyActionsQueryHandler(IExceptionRepository exceptions, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<MyActionsQuery, IReadOnlyList<MyActionItemDto>>
{
    /// <summary>Upper bound on findings read for one worklist; far above what one person carries.</summary>
    private const int MaxFindings = 500;

    public async Task<IReadOnlyList<MyActionItemDto>> Handle(MyActionsQuery query, CancellationToken cancellationToken)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var findings = await exceptions.ListFollowUpForUserAsync(me, MaxFindings, cancellationToken);

        var items = findings.SelectMany(e => ItemsFor(e, me, today)).ToList();

        // Overdue first, then soonest due; undated items last. Stable on finding then action for a steady list.
        return [.. items
            .OrderByDescending(i => i.IsOverdue)
            .ThenBy(i => i.DueDate ?? DateOnly.MaxValue)
            .ThenBy(i => i.ExceptionId)
            .ThenBy(i => i.ActionId)];
    }

    /// <summary>What one finding asks of <paramref name="me"/> today (several items when they hold several roles on it).</summary>
    public static IEnumerable<MyActionItemDto> ItemsFor(AuditException e, Guid me, DateOnly today)
    {
        var pendingFor = e.ExtensionRequests
            .Where(r => r.Status == ExtensionRequestStatus.Pending)
            .Select(r => r.ActionId)
            .ToHashSet();

        MyActionItemDto Item(string kind, Guid? actionId, Guid? requestId, string? description, DateOnly? due, bool extensionPending)
        {
            int? daysUntil = due is { } d ? d.DayNumber - today.DayNumber : null;
            return new MyActionItemDto(
                kind, e.Id, e.Title, e.Severity.ToSnake(), e.Status.ToSnake(), e.AuditId, actionId, requestId, description,
                due, daysUntil, daysUntil < 0, extensionPending);
        }

        if (e.OwnerUserId == me)
        {
            if (e.ManagementResponseDecision is null)
            {
                yield return Item("respond", null, null, null, e.ManagementResponseDueDate, false);
            }

            if (e.Status is ExceptionStatus.Open or ExceptionStatus.MapRejected)
            {
                yield return Item("submit_plan", null, null, e.MapRejectionReason, e.TargetDate, pendingFor.Contains(null));
            }

            var current = e.CurrentMapActions.ToList();
            if (e.Status == ExceptionStatus.MapApproved && current.Count > 0 && current.All(a => a.Status == MapActionStatus.Complete))
            {
                yield return Item("complete_plan", null, null, null, e.TargetDate, false);
            }
        }

        if (e.Status == ExceptionStatus.MapApproved)
        {
            foreach (var action in e.CurrentMapActions.Where(a => a.OwnerUserId == me && a.Status == MapActionStatus.Pending))
            {
                yield return Item("complete_action", action.Id, null, action.Description, action.TargetDate, pendingFor.Contains(action.Id));
            }
        }

        if (e.RaisedByUserId == me)
        {
            foreach (var request in e.ExtensionRequests.Where(r => r.Status == ExtensionRequestStatus.Pending && r.RequestedBy != me))
            {
                yield return Item("decide_extension", request.ActionId, request.Id, request.Reason, request.CurrentDate, false);
            }
        }
    }
}
