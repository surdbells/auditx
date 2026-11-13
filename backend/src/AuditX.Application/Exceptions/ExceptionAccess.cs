using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Execution;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Exceptions;

internal static class ExceptionAccess
{
    /// <summary>
    /// Audit-scoped authorization for exceptions. [RequirePermission] only gates globally; the resource scope
    /// is enforced here — the caller must be an active team member of the owning audit OR hold ManageAudit
    /// scoped to it. Throws 403 otherwise.
    /// </summary>
    public static async Task EnsureCanAccessAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not a member of this audit.");
    }

    /// <summary>
    /// AUDIT-SIDE actions on a finding — raise, re-rate, reassign, verify, reopen, cancel, link, escalate. As
    /// <see cref="EnsureCanAccessAsync"/>, and never by the audit's own auditee (independence).
    /// </summary>
    public static async Task EnsureCanActAsAuditorAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        AuditIndependence.EnsureNotAuditee(audit, userId);
        await EnsureCanAccessAsync(audit, userId, permissions, cancellationToken);
    }

    /// <summary>
    /// Who may READ one finding: anyone on its audit, a holder of ManageAudit for it, a holder of portfolio visibility
    /// (ViewAudits — they already see it in the tracker), and the finding's OWNER. The owner is named on the finding
    /// and must act on it, but is often not on the audit's team: without this they saw it in their tracker and got a
    /// 403 opening it.
    /// </summary>
    public static async Task EnsureCanViewAsync(
        Audit audit, Domain.Exceptions.AuditException exception, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (exception.OwnerUserId == uid || audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ViewAudits, null, cancellationToken)
            || await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not a party to this finding.");
    }

    /// <summary>
    /// Resolves the participant scope for a cross-audit finding query (the Exceptions tracker and its CSV export).
    /// A user with portfolio visibility — ViewAudits, held by Auditor/Audit Manager/Administrator — sees the whole
    /// register. Anyone else (e.g. an Auditee, whose role is "reads exceptions assigned to them") is restricted to
    /// findings they own or whose audit they are an active team member of. Returns the user id to scope to, or
    /// <c>null</c> for an unrestricted portfolio view. Without an authenticated user, throws 403.
    /// </summary>
    public static async Task<Guid?> ResolveParticipantScopeAsync(
        Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        return await permissions.HasPermissionAsync(uid, PermissionKeys.ViewAudits, null, cancellationToken)
            ? null
            : uid;
    }
}
