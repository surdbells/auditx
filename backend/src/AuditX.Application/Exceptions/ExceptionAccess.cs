using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Execution;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Exceptions;

/// <summary>How the caller may record a finding's management response.</summary>
public enum ManagementResponseRole
{
    /// <summary>The caller cannot record a response.</summary>
    None,

    /// <summary>The caller is management for this finding — its owner or an auditee of its audit — and responds in their own name.</summary>
    Management,

    /// <summary>The caller is on the audit side and may record management's response for them, marked as such.</summary>
    OnBehalf,
}

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

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ViewAudits, audit.Id.ToString(), cancellationToken)
            || await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not a party to this finding.");
    }

    /// <summary>
    /// Who may record a finding's management response. Management for a finding is its OWNER (who must remediate it)
    /// and the AUDITEES of its audit; they respond in their own name and need no audit role to do it. Someone on the
    /// audit side who holds ManageException and may work on the audit can record it on management's behalf (for
    /// example, a reply received by email); that response is marked as recorded on their behalf.
    /// </summary>
    public static async Task<ManagementResponseRole> ResolveManagementResponseRoleAsync(
        Audit audit, Domain.Exceptions.AuditException exception, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            return ManagementResponseRole.None;
        }

        if (exception.OwnerUserId == uid || audit.IsAuditee(uid))
        {
            return ManagementResponseRole.Management;
        }

        if (!await permissions.HasPermissionAsync(uid, PermissionKeys.ManageException, scopeValue: null, cancellationToken))
        {
            return ManagementResponseRole.None;
        }

        var onAudit = audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid)
            || await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken);
        return onAudit ? ManagementResponseRole.OnBehalf : ManagementResponseRole.None;
    }

    /// <summary>
    /// Resolves the participant scope for a cross-audit finding query (the Exceptions tracker and its CSV export).
    /// A user with portfolio visibility — ViewAudits, held by Auditor/Audit Manager/Administrator — sees the whole
    /// register. Anyone else (e.g. an Auditee, whose role is "reads exceptions assigned to them") is restricted to
    /// findings they own or whose audit they are an active team member of. Returns the user id to scope to, or
    /// <c>null</c> for an unrestricted portfolio view. Without an authenticated user, throws 403.
    /// </summary>
    /// <remarks>
    /// A caller whose ViewAudits is limited to some org units also sees the findings of audits in those units. The
    /// result is null for an organisation-wide view.
    /// </remarks>
    public static async Task<ListScope?> ResolveParticipantScopeAsync(
        Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
        => await OrgReach.ListScopeAsync(userId, PermissionKeys.ViewAudits, permissions, cancellationToken);
}
