namespace AuditX.Domain.Authorization;

/// <summary>
/// The abilities every person has on what they are NAMED on — as the auditee of an audit, a member of its team, the
/// owner of a finding or of an action, the person a document was requested from, or the owner of an area.
///
/// "Auditee" is a relationship to a particular audit, not a job: an auditor is the auditee when their own area is
/// audited. So these abilities are not a role an administrator assigns (and can forget to assign); the platform gives
/// them, automatically, to anyone it has named — and only for as long as, and only on, what they are named on. They
/// open nothing by themselves: every one of them is narrowed to the caller's involvement where it is used.
///
/// Someone who holds no role and is named on nothing has no permissions at all, and stays on "Access pending".
/// </summary>
public static class BaselineAccess
{
    /// <summary>The permission keys granted by involvement — exactly what the built-in Auditee role carried.</summary>
    public static readonly IReadOnlyList<string> Permissions = BuiltInRoles.Auditee.Permissions;

    private static readonly IReadOnlyDictionary<string, PermissionDefinition> Definitions =
        PermissionCatalogue.All.Where(p => Permissions.Contains(p.Key)).ToDictionary(p => p.Key);

    /// <summary>The catalogue definition (and with it the scope type) of each baseline permission.</summary>
    public static IEnumerable<PermissionDefinition> Catalogue => Permissions.Select(key => Definitions[key]);

    /// <summary>
    /// The built-in Auditee role is retired as something to hand out: what it granted is now automatic. It stays in the
    /// database so existing assignments keep resolving, but it can no longer be granted or delegated.
    /// </summary>
    public static bool IsRetiredRole(string roleName, bool isBuiltIn)
        => isBuiltIn && string.Equals(roleName, BuiltInRoles.AuditeeName, StringComparison.Ordinal);
}
