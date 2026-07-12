namespace AuditX.Domain.Identity;

/// <summary>
/// The maker-checker action types that are actually <em>enforced</em> today — i.e. have a live gate
/// decision point (a <c>TryCaptureAsync</c> call site) and a registered executor to replay the action
/// on approval. Only these are surfaced to admins as configurable dual-control toggles; the remaining
/// <see cref="MakerCheckerActionTypes"/> constants are placeholders for modules delivered later, and
/// toggling them would have no effect, so they are deliberately excluded until their gates are wired.
/// </summary>
public static class MakerCheckerActionCatalogue
{
    /// <summary>Action types with a live gate + executor, in the order shown to administrators.</summary>
    public static readonly IReadOnlyList<string> Enforced =
    [
        MakerCheckerActionTypes.RolePermissionChange,
        MakerCheckerActionTypes.TemplatePublish,
        MakerCheckerActionTypes.MapApproval,
        MakerCheckerActionTypes.SanctionsGridEdit,
        MakerCheckerActionTypes.ConfigActivation,
    ];

    /// <summary>True when the action type is one an administrator may configure a gate for.</summary>
    public static bool IsEnforced(string actionType) => Enforced.Contains(actionType);
}
