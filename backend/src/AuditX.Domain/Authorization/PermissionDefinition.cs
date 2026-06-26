using AuditX.Domain.Enums;

namespace AuditX.Domain.Authorization;

/// <summary>
/// One entry in the platform permission catalogue: a permission key with the metadata needed by
/// administrators to compose roles and by the SPA to render permission pickers.
/// </summary>
/// <param name="Key">Stable machine-readable key (see <see cref="PermissionKeys"/>).</param>
/// <param name="Label">Human-readable label for UI.</param>
/// <param name="Description">What holding the permission allows.</param>
/// <param name="Module">Owning module code, e.g. <c>M1</c>.</param>
/// <param name="FinestScope">
/// The narrowest scope at which the permission may be granted. <see cref="PermissionScopeType.Global"/>
/// means the permission is intrinsically global and cannot be narrowed.
/// </param>
public sealed record PermissionDefinition(
    string Key,
    string Label,
    string Description,
    string Module,
    PermissionScopeType FinestScope);
