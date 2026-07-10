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
        "Manages role assignments, custom roles, templates and bank-wide configuration; does not perform audit work.",
        [
            PermissionKeys.ManageUsers, PermissionKeys.ManageRoles, PermissionKeys.Delegate,
            PermissionKeys.ViewAuditTrail, PermissionKeys.ExportAuditTrail, PermissionKeys.ViewConfig,
            PermissionKeys.ManageConfiguration, PermissionKeys.ManageTemplates, PermissionKeys.ViewTemplates,
            PermissionKeys.ManageUniverse, PermissionKeys.ViewUniverse, PermissionKeys.ScoreRisk,
            PermissionKeys.ViewRisk, PermissionKeys.ManageRisk,
            PermissionKeys.ViewControls, PermissionKeys.ManageControls,
            PermissionKeys.ManagePlan, PermissionKeys.ViewPlan, PermissionKeys.ViewCoverage,
            PermissionKeys.ViewAudits, PermissionKeys.ViewAudit, PermissionKeys.CreateAudit, PermissionKeys.ManageAudit,
            PermissionKeys.RespondItem, PermissionKeys.UploadEvidence, PermissionKeys.ViewEvidence, PermissionKeys.ManageEvidence,
            PermissionKeys.LogTime, PermissionKeys.ViewTimeEntries,
            PermissionKeys.ViewExceptions, PermissionKeys.RaiseException, PermissionKeys.ManageException, PermissionKeys.SubmitMap,
            PermissionKeys.ApproveMap, PermissionKeys.CloseException, PermissionKeys.CancelException, PermissionKeys.Cia,
            PermissionKeys.ReopenException, PermissionKeys.VerifyException,
            PermissionKeys.ConfigureNotifications, PermissionKeys.ConfigureReports, PermissionKeys.ScheduleReports, PermissionKeys.ConfigureExceptionWorkflow,
            // Bootstrap governance: the super-user can approve annual plans + read AC surfaces out of the box, so a
            // fresh install can complete plan → approve → launch without first hand-assigning an Audit Committee Chair.
            PermissionKeys.AcMember, PermissionKeys.AcChair,
            PermissionKeys.ManageGrid, PermissionKeys.ViewGrid,
            PermissionKeys.ConfigureDashboards, PermissionKeys.ConfigurePredictive,
            PermissionKeys.ViewIntegrations, PermissionKeys.ConfigureIntegrations, PermissionKeys.ViewIntegrationHealth, PermissionKeys.ConfigureWebhooks,
            PermissionKeys.ViewBankSettings, PermissionKeys.ManageBankSettings, PermissionKeys.ViewSystemHealth,
            PermissionKeys.ManageSupportChannel, PermissionKeys.ManageRetention, PermissionKeys.ExecRestore,
            PermissionKeys.InstallReleases, PermissionKeys.ConfigureLimits, PermissionKeys.AdminOps,
        ]);

    public static readonly IReadOnlyList<BuiltInRoleDefinition> All = [Auditor, AuditManager, Auditee, Administrator];
}
