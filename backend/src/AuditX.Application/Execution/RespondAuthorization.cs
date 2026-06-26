using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Execution;

internal static class RespondAuthorization
{
    /// <summary>
    /// US-M5-014 + BR-M5-010. A response is permitted when the caller is an active team member and the item
    /// is unassigned or assigned to them, OR the caller holds ManageAudit (manager override). Returns true
    /// when this is a manager override of another auditor's assigned item (recorded distinctly in the trail).
    /// </summary>
    public static async Task<bool> EnsureCanRespondAsync(Audit audit, AuditChecklistItem item, Guid userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        var isManager = await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken);
        var isTeam = audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId);
        var assignedToOther = item.AssignedUserId is { } assignee && assignee != userId;
        var ownAssignmentOrUnassigned = item.AssignedUserId is null || item.AssignedUserId == userId;

        var allowed = isManager || (isTeam && ownAssignmentOrUnassigned);
        if (!allowed)
        {
            throw new ForbiddenAccessException("You are not permitted to respond to this checklist item.");
        }

        return isManager && assignedToOther;
    }

    public static ResponseVerdict? ParseVerdict(string? value, bool isDraft)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Replace("_", string.Empty).ToLowerInvariant() switch
        {
            "pass" => ResponseVerdict.Pass,
            "fail" => ResponseVerdict.Fail,
            "na" => ResponseVerdict.Na,
            _ => throw new DomainException("response.invalid_verdict", $"Unknown verdict '{value}'."),
        };
    }
}
