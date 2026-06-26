namespace AuditX.Domain.Identity;

/// <summary>
/// Logical action types that may be placed behind a maker-checker (dual-control) gate. The default
/// v2.0 configuration gates the set below; M1 enforces <see cref="RolePermissionChange"/>, the rest
/// are enforced as their owning modules are delivered.
/// </summary>
public static class MakerCheckerActionTypes
{
    public const string RolePermissionChange = "role_permission_change";
    public const string TemplatePublish = "template_publish";
    public const string PlanApproval = "plan_approval";
    public const string MapApproval = "map_approval";
    public const string SanctionApplication = "sanction_application";
    public const string SanctionsGridEdit = "sanctions_grid_edit";
    public const string RetentionPolicyChange = "retention_policy_change";

    /// <summary>Action types enabled for maker-checker by default on deployment.</summary>
    public static readonly IReadOnlyList<string> DefaultEnabled =
    [
        RolePermissionChange, TemplatePublish, PlanApproval, MapApproval,
        SanctionApplication, SanctionsGridEdit, RetentionPolicyChange,
    ];
}
