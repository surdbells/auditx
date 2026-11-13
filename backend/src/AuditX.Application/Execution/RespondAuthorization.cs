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
    ///         holding a permission (even ManageAudit, even the administrator's full set) is not enough on its own.
    ///         The AUDITEE is listed on the audit but is its subject, never part of the working team: someone who is
    ///         an auditor elsewhere gets no say over the checklist of the audit of their own area;</item>
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

        // Independence: the person being audited never answers the audit's own checklist, whatever roles they hold.
        if (IsAuditee(audit, userId))
        {
            throw new ForbiddenAccessException("You are the auditee of this audit, so you cannot work on its checklist — an audit must be independent of the area it examines.");
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

    /// <summary>
    /// The audit's WORKING team: an active member in an auditing capacity (lead / auditor / reviewer). The auditee is
    /// recorded on the audit's team list so they can follow it, but they are the subject of the audit, not part of
    /// the team that performs it.
    /// </summary>
    public static bool IsActiveTeamMember(Audit audit, Guid userId)
        => !IsAuditee(audit, userId)
            && audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId && m.TeamRole != TeamRole.Auditee);

    /// <summary>True when the user is the auditee of this (non-self-assessment) audit.</summary>
    public static bool IsAuditee(Audit audit, Guid userId)
        => !audit.IsSelfAssessment
            && (audit.AuditeeUserId == userId
                || audit.TeamMembers.Any(m => m.IsActive && m.UserId == userId && m.TeamRole == TeamRole.Auditee));

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
