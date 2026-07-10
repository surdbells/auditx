using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;

namespace AuditX.Application.Evidence;

internal static class EvidenceRequestConcurrency
{
    public static void EnsureVersion(this EvidenceRequest request, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(request.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("evidence_request.concurrency_conflict", "The evidence request was modified by someone else; reload and retry.");
        }
    }
}

internal static class EvidenceRequestAccess
{
    /// <summary>Audit phases in which evidence may be requested / actioned (a launched, non-terminal audit).</summary>
    private static readonly AuditStatus[] Actionable = [AuditStatus.Planned, AuditStatus.InProgress, AuditStatus.UnderReview];

    private static bool IsTeamMember(Audit audit, Guid uid)
        => audit.TeamMembers.Any(m => m.IsActive && m.UserId == uid);

    /// <summary>Mutation authorization: an active team member or audit manager, and the audit is in an actionable phase.</summary>
    public static async Task EnsureCanActionAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        await EnsureTeamOrManagerAsync(audit, userId, permissions, cancellationToken);
        if (!Actionable.Contains(audit.Status))
        {
            throw new ConflictException("evidence_request.audit_not_actionable", "Evidence can only be requested against a launched, non-completed audit.");
        }
    }

    /// <summary>View authorization: an active team member or an audit manager.</summary>
    public static Task EnsureCanViewAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
        => EnsureTeamOrManagerAsync(audit, userId, permissions, cancellationToken);

    private static async Task EnsureTeamOrManagerAsync(Audit audit, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        if (IsTeamMember(audit, uid))
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
