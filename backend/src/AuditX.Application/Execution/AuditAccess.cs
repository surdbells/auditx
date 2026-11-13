using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Execution;

internal static class AuditAccess
{
    /// <summary>
    /// Audit-scoped read/evidence authorization. The [RequirePermission] attribute only checks the
    /// permission globally; the resource scope is enforced here: the caller must be an active team member
    /// of the audit OR hold ManageAudit scoped to it. Throws 403 otherwise.
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
    /// Who may READ an audit — its detail, team, checklist, responses, progress, review figures and history.
    /// The controller's ViewAudit check is global, so the resource scope is enforced here. The caller must be:
    /// <list type="bullet">
    ///   <item>on the audit (any team role, including the auditee), or</item>
    ///   <item>a holder of ManageAudit for it, or</item>
    ///   <item>a holder of portfolio visibility — ViewAudits, the permission that already lists every audit
    ///         (Auditor / Audit Manager / Administrator).</item>
    /// </list>
    /// ViewAudit on its own — all an Auditee holds — therefore opens only the audits its holder is on. Without this
    /// an auditee could read any other area's audit (checklist, answers, findings in progress) from its id alone.
    /// Mirrors <c>ExceptionAccess.ResolveParticipantScopeAsync</c>. Throws 403 otherwise.
    /// </summary>
    public static async Task EnsureCanViewAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ViewAudits, null, cancellationToken)
            || await permissions.HasPermissionAsync(uid, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You are not a member of this audit.");
    }
}
