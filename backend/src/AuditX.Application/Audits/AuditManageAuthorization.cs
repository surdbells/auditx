using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
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

        if (await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, audit.Id.ToString(), cancellationToken))
        {
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
