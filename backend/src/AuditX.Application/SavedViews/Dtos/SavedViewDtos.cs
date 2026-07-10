namespace AuditX.Application.SavedViews.Dtos;

/// <summary>
/// A saved filter/parameter set (D3-A). <c>IsOwner</c> is resolved for the requesting user — only owners see the
/// edit / delete / share controls; a non-owner may still apply a shared view.
/// </summary>
public sealed record SavedViewDto(
    Guid Id,
    Guid OwnerUserId,
    string ViewKey,
    string Name,
    string ParametersJson,
    bool IsShared,
    bool IsOwner,
    string Version);
