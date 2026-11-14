namespace AuditX.Application.Abstractions.Authorization;

/// <summary>
/// Where in the organisation a user holds a permission. A role can be granted for an org unit, and each permission in
/// the role is either "whole organisation" or "assigned org unit"; this is the resulting reach of one permission key.
/// <see cref="OrgUnitIds"/> already includes every unit BELOW the units the role was granted for.
/// </summary>
/// <param name="Everywhere">Held for the whole organisation (a global grant, or a grant not limited to an org unit).</param>
/// <param name="OrgUnitIds">When not everywhere: the org units (and their sub-units) the permission is limited to.</param>
public sealed record OrgScope(bool Everywhere, IReadOnlyCollection<Guid> OrgUnitIds)
{
    /// <summary>Not held at all.</summary>
    public static readonly OrgScope Nowhere = new(false, []);

    /// <summary>Held for the whole organisation.</summary>
    public static readonly OrgScope All = new(true, []);

    /// <summary>Held, but only inside some org units.</summary>
    public bool IsLimited => !Everywhere && OrgUnitIds.Count > 0;

    /// <summary>Held anywhere at all.</summary>
    public bool IsHeld => Everywhere || OrgUnitIds.Count > 0;

    /// <summary>Whether something that belongs to <paramref name="orgUnitId"/> is within reach. Unassigned things are reachable only everywhere.</summary>
    public bool Covers(Guid? orgUnitId) => Everywhere || (orgUnitId is { } id && OrgUnitIds.Contains(id));
}
