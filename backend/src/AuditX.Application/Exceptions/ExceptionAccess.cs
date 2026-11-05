using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
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
