using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Common.Exceptions;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;
using AuditX.Domain.Sanctions;

namespace AuditX.Application.Sanctions;

/// <summary>
/// Confidentiality predicate for sanctions cases (US-M7-014/015). <c>[RequirePermission(ViewSanctions)]</c> gates
/// EXISTENCE; whether the subject identity is unmasked is decided here by case-team membership, with timing rules so
/// the right cohort can act on the case at the right stage.
/// </summary>
public static class SanctionsAccess
{
    /// <summary>
    /// True if the user may see the unmasked subject. The case team is: the subject themselves, any stored team
    /// member (investigator/recommender/HR-recorder), a <c>RecordHrOutcome</c> holder while the case is in
    /// <c>recommendation_submitted</c>/<c>hr_outcome_recorded</c> (A4a — HR must read the case before recording the
    /// outcome), or a <c>DCMember</c> holder while the case is in <c>dc_referral</c>/<c>dc_decision_recorded</c>.
    /// </summary>
    public static async Task<bool> IsCaseTeamMemberAsync(
        SanctionsCase sanctionsCase, Guid? userId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            return false;
        }

        if (sanctionsCase.SubjectUserId == uid)
        {
            return true;
        }

        if (sanctionsCase.TeamMembers.Any(m => m.UserId == uid))
        {
            return true;
        }

        if (sanctionsCase.Status is SanctionsCaseStatus.RecommendationSubmitted or SanctionsCaseStatus.HrOutcomeRecorded
            && await permissions.HasPermissionAsync(uid, PermissionKeys.RecordHrOutcome, scopeValue: null, cancellationToken))
        {
            return true;
        }

        if (sanctionsCase.Status is SanctionsCaseStatus.DcReferral or SanctionsCaseStatus.DcDecisionRecorded
            && await permissions.HasPermissionAsync(uid, PermissionKeys.DcMember, scopeValue: null, cancellationToken))
        {
            return true;
        }

        return false;
    }
}

internal static class SanctionsConcurrency
{
    public static void EnsureVersion(this SanctionsCase c, string expectedVersion)
    {
        if (!string.Equals(Convert.ToBase64String(c.Version ?? []), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("sanctions.concurrency_conflict", "The case was modified by someone else; reload and retry.");
        }
    }

    public static void EnsureVersion(this SanctionsAppeal a, string expectedVersion)
    {
        if (!string.Equals(Convert.ToBase64String(a.Version ?? []), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("sanctions.concurrency_conflict", "The appeal was modified by someone else; reload and retry.");
        }
    }
}
