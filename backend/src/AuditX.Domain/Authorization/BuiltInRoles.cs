namespace AuditX.Domain.Authorization;

/// <summary>Definition of a built-in role: its name, description and default v2.0 permission set.</summary>
public sealed record BuiltInRoleDefinition(string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// The four built-in roles seeded on deployment (US-M1-010). They map to the core personas in the
/// BRD. Built-in roles are immutable (<c>is_builtin = true</c>); a bank that needs a variation clones
/// one into a custom role rather than editing it.
/// </summary>
public static class BuiltInRoles
{
    public const string AuditorName = "Auditor";
    public const string AuditManagerName = "Audit Manager";
    public const string AuditeeName = "Auditee";
    public const string AdministratorName = "AuditX Administrator";

    public static readonly BuiltInRoleDefinition Auditor = new(
        AuditorName,
        "Performs audit execution work and raises exceptions; cannot create audits.",
        [
            PermissionKeys.ViewAudits, PermissionKeys.ViewAudit, PermissionKeys.ViewTemplates,
            PermissionKeys.ViewUniverse, PermissionKeys.RespondItem, PermissionKeys.UploadEvidence,
            PermissionKeys.ViewEvidence, PermissionKeys.ViewExceptions, PermissionKeys.RaiseException,
            PermissionKeys.SubmitMap, PermissionKeys.VerifyException, PermissionKeys.ViewReport, PermissionKeys.ViewAnalytics,
            PermissionKeys.LogTime, PermissionKeys.ViewTimeEntries, PermissionKeys.ViewRisk, PermissionKeys.ViewControls,
        ]);

    public static readonly BuiltInRoleDefinition AuditManager = new(
        AuditManagerName,
        "Owns the annual audit plan; creates and leads audits, reviews work, approves MAPs, closes exceptions and generates reports.",
        [
            PermissionKeys.ViewAudits, PermissionKeys.ViewAudit, PermissionKeys.CreateAudit, PermissionKeys.ManageAudit,
            PermissionKeys.ViewTemplates, PermissionKeys.ViewUniverse, PermissionKeys.ViewPlan, PermissionKeys.ManagePlan, PermissionKeys.ViewCoverage,
            PermissionKeys.RespondItem, PermissionKeys.UploadEvidence, PermissionKeys.ViewEvidence, PermissionKeys.ManageEvidence,
            PermissionKeys.ViewExceptions, PermissionKeys.RaiseException, PermissionKeys.ManageException,
            PermissionKeys.ApproveMap, PermissionKeys.CloseException, PermissionKeys.CancelException,
            PermissionKeys.ReopenException, PermissionKeys.VerifyException,
            PermissionKeys.GenerateReport, PermissionKeys.ViewReport, PermissionKeys.DistributeReport, PermissionKeys.ScheduleReports,
            PermissionKeys.ViewSanctions, PermissionKeys.TriggerSanctions, PermissionKeys.RecommendSanction, PermissionKeys.ViewGrid,
            PermissionKeys.ViewAnalytics, PermissionKeys.AdHocQueryUse, PermissionKeys.PerformanceAnalyticsView, PermissionKeys.ViewAuditTrail,
            PermissionKeys.LogTime, PermissionKeys.ViewTimeEntries, PermissionKeys.ViewRisk, PermissionKeys.ManageRisk,
            PermissionKeys.ViewControls, PermissionKeys.ManageControls,
        ]);

    public static readonly BuiltInRoleDefinition Auditee = new(
        AuditeeName,
        "Reads exceptions assigned to them, submits MAPs and uploads remediation evidence.",
        [
            PermissionKeys.ViewAudit, PermissionKeys.ViewExceptions, PermissionKeys.SubmitMap,
            PermissionKeys.UploadEvidence, PermissionKeys.ViewEvidence, PermissionKeys.ViewReport,
        ]);

    public static readonly BuiltInRoleDefinition Administrator = new(
        AdministratorName,
        "The super-user: holds every permission in the catalogue, including audit execution and all administration.",
        // The AuditX Administrator is granted ALL permissions — the full catalogue, so nothing in the platform
        // is ever gated away from the super-user. New permissions are picked up automatically as they are added.
        [.. PermissionCatalogue.All.Select(p => p.Key)]);

    public static readonly IReadOnlyList<BuiltInRoleDefinition> All = [Auditor, AuditManager, Auditee, Administrator];
}
