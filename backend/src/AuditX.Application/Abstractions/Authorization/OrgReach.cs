using AuditX.Application.Common.Exceptions;

namespace AuditX.Application.Abstractions.Authorization;

/// <summary>
/// The resource-scope guard for a list when the caller's permission is limited to some org units: they see what they
/// are a party to (on the audit's team, its auditee, the finding's owner) plus what belongs to those units.
/// </summary>
public sealed record ListScope(Guid UserId, IReadOnlyCollection<Guid> OrgUnitIds);

/// <summary>Applies org-unit limited grants to lists and to writes that place something in an org unit.</summary>
public static class OrgReach
{
    /// <summary>
    /// The guard for a list gated by <paramref name="permissionKey"/>: null when the caller holds it for the whole
    /// organisation (no narrowing), otherwise the units it is limited to.
    /// </summary>
    public static async Task<ListScope?> ListScopeAsync(
        Guid? userId, string permissionKey, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        var scope = await permissions.GetOrgScopeAsync(uid, permissionKey, cancellationToken);
        return scope is null || scope.Everywhere ? null : new ListScope(uid, scope.OrgUnitIds);
    }

    /// <summary>
    /// Refuses a write that would place (or keep) something in an org unit outside the caller's reach for
    /// <paramref name="permissionKey"/>. Something in no org unit is reachable only with an organisation-wide grant.
    /// </summary>
    public static async Task EnsureCoversAsync(
        Guid? userId, string permissionKey, Guid? orgUnitId, IPermissionResolver permissions, CancellationToken cancellationToken)
    {
        if (userId is not { } uid)
        {
            throw new ForbiddenAccessException();
        }

        var scope = await permissions.GetOrgScopeAsync(uid, permissionKey, cancellationToken);
        if (scope is null || scope.Covers(orgUnitId))
        {
            return;
        }

        throw new ForbiddenAccessException(orgUnitId is null
            ? "Your role applies only to some org units, so what you create or change must belong to one of them. Choose an org unit you are responsible for."
            : "That belongs to an org unit outside the ones your role applies to.");
    }
}
