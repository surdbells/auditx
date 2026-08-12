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
    /// US-M5-014 + BR-M5-010. On a normal audit a response is permitted when the caller holds RespondItem AND is
    /// an active team member with the item unassigned or assigned to them, OR the caller holds ManageAudit
    /// (manager override). On a self-assessment only the self-assessor (with RunSelfAssessment) may respond — the
    /// area owner completes their own assessment, with no independent auditor. Returns true when this is a manager
    /// override of another auditor's assigned item (recorded distinctly in the trail). The permission gate lives
    /// here (not on the controller) so both flows are authorised in one place.
    /// </summary>
    public static async Task<bool> EnsureCanRespondAsync(Audit audit, AuditChecklistItem item, Guid userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (audit.IsSelfAssessment)
        {
            var isAssessor = audit.LeadUserId == userId;
            var canSelfAssess = await permissions.HasPermissionAsync(userId, PermissionKeys.RunSelfAssessment, audit.Id.ToString(), cancellationToken);
            if (!isAssessor || !canSelfAssess)
            {
                throw new ForbiddenAccessException("Only the self-assessor may respond to their own self-assessment.");
            }

            return false;
        }

        var isManager = await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken);
        var canRespond = await permissions.HasPermissionAsync(userId, PermissionKeys.RespondItem, audit.Id.ToString(), cancellationToken);
        var isTeam = audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId);
        var assignedToOther = item.AssignedUserId is { } assignee && assignee != userId;
        var ownAssignmentOrUnassigned = item.AssignedUserId is null || item.AssignedUserId == userId;

        var allowed = isManager || (canRespond && isTeam && ownAssignmentOrUnassigned);
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
