using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Execution;
using AuditX.Domain.Audits;
using AuditX.Domain.Authorization;

namespace AuditX.Application.Audits;

internal static class AuditManageAuthorization
{
    /// <summary>
    /// Whether the caller may drive an audit's lifecycle. A normal audit requires ManageAudit (scoped to this
    /// audit). A self-assessment is instead driven by its self-assessor (the lead, who holds RunSelfAssessment),
    /// so they can move their own assessment Draft → In Progress → Completed without ManageAudit. Enforced here
    /// (not on the controller) so both flows are authorised in one place.
    /// </summary>
    public static async Task EnsureCanManageAsync(Audit audit, Guid? callerUserId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        var userId = callerUserId ?? throw new ForbiddenAccessException();

        // Independence: the audit's own auditee never drives it, even holding ManageAudit. (Not a self-assessment's
        // assessor — IsAuditee is false there.)
        AuditIndependence.EnsureNotAuditee(audit, userId);

        if (await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
            // Holding ManageAudit is not enough on its own: an audit is planned, started, sent for review, completed
            // or reopened by its OWN team (lead / auditor / reviewer). A manager or administrator who is not on the
            // team supervises it but does not sign it off — the same rule as checklist work, with no override.
            if (!audit.IsSelfAssessment && !Execution.RespondAuthorization.IsActiveTeamMember(audit, userId))
            {
                throw new ForbiddenAccessException(
                    "Only members of this audit's team can move it through its lifecycle. Ask the audit lead, or have yourself added to the team while it is being planned.");
            }

            return;
        }

        if (audit.IsSelfAssessment
            && audit.LeadUserId == userId
            && await permissions.HasPermissionAsync(userId, PermissionKeys.RunSelfAssessment, audit.Id.ToString(), cancellationToken))
        {
            return;
        }

        throw new ForbiddenAccessException("You do not manage this audit.");
    }
}
