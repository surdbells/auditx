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
    /// Who may work a checklist item — respond to it, save or discard its draft, attach evidence to its response
    /// (US-M5-014 + BR-M5-010). The gate lives here (not on the controller) so every path is authorised in one place.
    ///
    /// On a normal audit, ALL of these must hold:
    /// <list type="number">
    ///   <item>the caller is an ACTIVE MEMBER OF THE AUDIT'S TEAM — checklist work belongs to the engagement team;
    ///         holding a permission (even ManageAudit, even the administrator's full set) is not enough on its own;</item>
    ///   <item>if the item is ASSIGNED, the caller is the assignee — an assigned item is answered by the person it was
    ///         assigned to and nobody else; to hand it to someone else a manager reassigns it (which is recorded);</item>
    ///   <item>the caller holds RespondItem or ManageAudit for the audit.</item>
    /// </list>
    /// On a self-assessment only the self-assessor (with RunSelfAssessment) may respond — the area owner completes
    /// their own assessment, with no independent auditor.
    /// </summary>
    public static async Task EnsureCanRespondAsync(Audit audit, AuditChecklistItem item, Guid userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (audit.IsSelfAssessment)
        {
            var isAssessor = audit.LeadUserId == userId;
            var canSelfAssess = await permissions.HasPermissionAsync(userId, PermissionKeys.RunSelfAssessment, audit.Id.ToString(), cancellationToken);
            if (!isAssessor || !canSelfAssess)
            {
                throw new ForbiddenAccessException("Only the self-assessor may respond to their own self-assessment.");
            }

            return;
        }

        if (!IsActiveTeamMember(audit, userId))
        {
            throw new ForbiddenAccessException("Only members of this audit's team can work on its checklist items.");
        }

        if (item.AssignedUserId is { } assignee && assignee != userId)
        {
            throw new ForbiddenAccessException("This item is assigned to another team member — only they can respond to it.");
        }

        var canRespond = await permissions.HasPermissionAsync(userId, PermissionKeys.RespondItem, audit.Id.ToString(), cancellationToken)
            || await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken);
        if (!canRespond)
        {
            throw new ForbiddenAccessException("You are not permitted to respond to this checklist item.");
        }
    }

    public static bool IsActiveTeamMember(Audit audit, Guid userId)
        => audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId);

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
