using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Reports;

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

    /// <summary>
    /// Authorizes a STANDALONE (cross-audit) report operation. There is no audit to scope against, so access is
    /// permission-only: the caller must hold <see cref="PermissionKeys.ViewAnalytics"/> (in addition to the
    /// <c>ViewReport</c>/<c>GenerateReport</c> permission the controller gates globally), because a standalone report
    /// exposes function-wide analytics. Throws 403 otherwise.
    /// </summary>
    public static async Task EnsureCanAccessStandaloneAsync(Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (await permissions.HasPermissionAsync(uid, PermissionKeys.ViewAnalytics, null, cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("Standalone reports require the analytics permission.");
    }

    /// <summary>
    /// Authorizes a read/download of an existing report by branching on its shape: an engagement report is
    /// audit-scoped (member OR <c>ManageAudit</c>); a standalone report is permission-only (analytics). Centralises
    /// the null-<c>AuditId</c> branch so every report read query gates identically.
    /// </summary>
    public static async Task EnsureCanReadAsync(
        Report report, IAuditRepository audits, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (report.AuditId is { } auditId)
        {
            var audit = await audits.GetByIdAsync(auditId, cancellationToken) ?? throw new NotFoundException("Audit", auditId);
            await EnsureCanAccessAsync(audit, userId, permissions, cancellationToken);
        }
        else
        {
            await EnsureCanAccessStandaloneAsync(userId, permissions, cancellationToken);
        }
    }
}
