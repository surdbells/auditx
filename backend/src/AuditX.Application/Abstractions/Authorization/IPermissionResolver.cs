using AuditX.Domain.Enums;

namespace AuditX.Application.Abstractions.Authorization;

/// <summary>An effective permission a user holds, with the scope at which it applies.</summary>
public sealed record EffectivePermission(string Key, PermissionScopeType ScopeType, string? ScopeValue, string? ScopePredicateJson);

/// <summary>
/// Resolves a user's effective permissions — the transitive union of all their role assignments,
/// role inheritance and currently-active delegations. Results are cached (Redis, short TTL) and
/// invalidated when the user's assignments or any relevant role changes.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if the user holds <paramref name="permissionKey"/>. When <paramref name="scopeValue"/> is
    /// supplied, a scoped grant must match it (a global grant always satisfies a scoped check).
    /// </summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionKey, string? scopeValue = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where in the organisation the user holds <paramref name="permissionKey"/>: everywhere, only inside some org
    /// units (a role granted for an org unit, with the permission set to "assigned org unit"), or nowhere. Lists use it
    /// to show only what is within reach.
    /// </summary>
    Task<OrgScope> GetOrgScopeAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The org units the user's grants of <paramref name="permissionKey"/> that are limited to an org unit reach: each
    /// unit a grant was made for and every unit below it. Grants that apply everywhere or name one resource are not
    /// counted, so a caller combines this with those. Empty when there are no such grants.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetLimitedOrgUnitsAsync(Guid userId, string permissionKey, CancellationToken cancellationToken = default);

    /// <summary>Invalidate any cached permissions for the user (after role/delegation changes).</summary>
    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Invalidate cached permissions for every user (after a role definition changes).</summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
