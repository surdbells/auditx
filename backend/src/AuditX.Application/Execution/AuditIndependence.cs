using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Audits;

namespace AuditX.Application.Execution;

/// <summary>
/// The independence rule: the AUDITEE of an audit acts on it only as its auditee, whatever other roles they hold.
///
/// Roles are global, and an auditor or audit manager is the auditee whenever their own area is audited. Without this
/// rule their auditor powers would apply to the audit of themselves — running its fieldwork, raising or closing its
/// findings, approving their own action plan, issuing its report. Every AUDIT-SIDE action therefore refuses the
/// audit's auditee; the AUDITEE-SIDE actions (answer a document request, submit and complete the action plan for a
/// finding they own, read what concerns them) stay open to them.
///
/// A self-assessment has no independent auditor by design — the area owner assesses their own area — so the rule
/// does not apply to it.
/// </summary>
public static class AuditIndependence
{
    public const string Message =
        "You are the auditee of this audit, so you can only act on it as its auditee — an audit must be independent of the area it examines.";

    /// <summary>True when the user is the auditee of this (non-self-assessment) audit.</summary>
    public static bool IsAuditee(Audit audit, Guid userId) => RespondAuthorization.IsAuditee(audit, userId);

    /// <summary>Refuses an audit-side action attempted by the audit's own auditee (403).</summary>
    public static void EnsureNotAuditee(Audit audit, Guid? userId)
    {
        if (userId is { } uid && IsAuditee(audit, uid))
        {
            throw new ForbiddenAccessException(Message);
        }
    }
}
