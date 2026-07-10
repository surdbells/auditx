using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.TimeTracking;

namespace AuditX.Application.TimeTracking;

internal static class TimeEntryParsing
{
    // Uses the shared guarded parser (rejects undefined enum values like "99", not just unknown names).
    public static TimeEntryCategory ParseCategory(string? value)
        => EnumExtensions.TryParseSnake<TimeEntryCategory>(value, out var c)
            ? c
            : throw new DomainException("time_entry.invalid_category", $"Unknown time category '{value}'.");
}

internal static class TimeEntryConcurrency
{
    public static void EnsureVersion(this TimeEntry entry, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(entry.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("time_entry.concurrency_conflict", "The time entry was modified by someone else; reload and retry.");
        }
    }
}

internal static class TimeEntryAccess
{
    /// <summary>Statuses in which time may be logged — a launched audit that is not cancelled.</summary>
    private static readonly AuditStatus[] Loggable =
        [AuditStatus.Planned, AuditStatus.InProgress, AuditStatus.UnderReview, AuditStatus.Completed];

    /// <summary>
    /// True if the user is an active audit-team WORKER (Lead / Auditor / Reviewer). Excludes the Auditee: the
    /// auditee is the audited party (always a permanent team member) and must not see or log the team's effort,
    /// budget or per-auditor utilisation — they hold ViewAudit but deliberately not ViewTimeEntries.
    /// </summary>
    private static bool IsAuditWorker(Audit audit, Guid uid)
        => audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid && m.TeamRole != TeamRole.Auditee);

    /// <summary>
    /// Audit-scoped authorization for logging time. [RequirePermission(LogTime)] gates globally; the resource
    /// scope is enforced here — the caller must be an active team member of the audit OR hold ManageAudit
    /// scoped to it. Also rejects logging against a Draft or Cancelled audit.
    /// </summary>
    public static async Task EnsureCanLogAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        await EnsureTeamOrManagerAsync(audit, userId, permissions, cancellationToken);
        if (!Loggable.Contains(audit.Status))
        {
            throw new ConflictException("time_entry.audit_not_loggable", "Time can only be logged against a launched, non-cancelled audit.");
        }
    }

    /// <summary>
    /// View authorization for an audit's time entries + summary: an active team member, an audit manager,
    /// or anyone holding the global ViewTimeEntries permission (analysts / management).
    /// </summary>
    public static async Task EnsureCanViewAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (IsAuditWorker(audit, uid))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ViewTimeEntries, scopeValue: null, cancellationToken)
            || await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not permitted to view this audit's time.");
    }

    /// <summary>Amend/delete authorization: only the entry's owner or an audit manager may modify it.</summary>
    public static async Task EnsureCanModifyAsync(TimeEntry entry, Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (entry.UserId == uid)
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("Only the entry's owner or an audit manager can modify this time entry.");
    }

    private static async Task EnsureTeamOrManagerAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (IsAuditWorker(audit, uid))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not a member of this audit.");
    }
}
