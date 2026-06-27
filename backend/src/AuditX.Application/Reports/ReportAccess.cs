using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Reports;

/// <summary>
/// Audit-scoped authorization for report operations (M8). The <c>[RequirePermission]</c> attribute only checks
/// the report permission globally; the resource scope is enforced here: the caller must be an active team member
/// of the report's audit OR hold <c>ManageAudit</c> scoped to it. Mirrors the M5 <c>AuditAccess</c> predicate so
/// Auditee/Auditor <c>ViewReport</c> resolves to the audit they belong to. Throws 403 otherwise.
/// </summary>
public static class ReportAccess
{
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
}
