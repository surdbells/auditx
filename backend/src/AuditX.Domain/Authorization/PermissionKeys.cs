namespace AuditX.Domain.Authorization;

/// <summary>
/// Stable, machine-readable permission keys for the whole platform. Permissions are finer-grained
/// than CRUD and are the unit of authorisation enforced on every protected operation. Keys are
/// referenced from role definitions, the <c>[RequirePermission]</c> attribute, and the seed data.
/// The full catalogue (with labels, descriptions and scope metadata) lives in
/// <see cref="PermissionCatalogue"/>.
/// </summary>
public static class PermissionKeys
{
    // M1 — Identity, Roles & Permissions
    public const string ManageUsers = "ManageUsers";
    public const string ManageRoles = "ManageRoles";
    public const string Delegate = "Delegate";
    public const string ViewAuditTrail = "ViewAuditTrail";
    public const string ExportAuditTrail = "ExportAuditTrail";
    public const string ViewConfig = "ViewConfig";

    // M2 — Templates
    public const string ViewTemplates = "ViewTemplates";
    public const string ManageTemplates = "ManageTemplates";

    // M3 — Universe & Planning
    public const string ViewUniverse = "ViewUniverse";
    public const string ManageUniverse = "ManageUniverse";
    public const string ScoreRisk = "ScoreRisk";
    public const string ViewPlan = "ViewPlan";
    public const string ManagePlan = "ManagePlan";
    public const string ViewCoverage = "ViewCoverage";

    // M4 — Audits
    public const string ViewAudits = "ViewAudits";
    public const string ViewAudit = "ViewAudit";
    public const string CreateAudit = "CreateAudit";
    public const string ManageAudit = "ManageAudit";

    // M5 — Execution
    public const string RespondItem = "RespondItem";
    public const string UploadEvidence = "UploadEvidence";
    public const string ViewEvidence = "ViewEvidence";
    public const string ManageEvidence = "ManageEvidence";

    // M6 — Exceptions
    public const string ViewExceptions = "ViewExceptions";
    public const string RaiseException = "RaiseException";
    public const string ManageException = "ManageException";
    public const string SubmitMap = "SubmitMap";
    public const string ApproveMap = "ApproveMap";
    public const string CloseException = "CloseException";
    public const string CancelException = "CancelException";
    public const string ConfigureExceptionWorkflow = "ConfigureExceptionWorkflow";

    // M7 — Sanctions
    public const string ViewSanctions = "ViewSanctions";
    public const string TriggerSanctions = "TriggerSanctions";
    public const string RecommendSanction = "RecommendSanction";
    public const string RecordHrOutcome = "RecordHROutcome";
    public const string ReferToDc = "ReferToDC";
    public const string DcMember = "DCMember";
    public const string FileAppeal = "FileAppeal";
    public const string DecideAppeal = "DecideAppeal";
    public const string ManageGrid = "ManageGrid";
    public const string ViewGrid = "ViewGrid";

    // M8 — Reports
    public const string GenerateReport = "GenerateReport";
    public const string ViewReport = "ViewReport";
    public const string DistributeReport = "DistributeReport";
    public const string ConfigureReports = "ConfigureReports";

    // M9 — Analytics
    public const string ViewAnalytics = "ViewAnalytics";
    public const string AdHocQueryUse = "AdHocQueryUse";
    public const string ConfigureDashboards = "ConfigureDashboards";
    public const string ConfigurePredictive = "ConfigurePredictive";
    public const string PerformanceAnalyticsView = "PerformanceAnalyticsView";

    // M10 — Notifications
    public const string ConfigureNotifications = "ConfigureNotifications";

    // M12 — Configuration
    public const string ManageConfiguration = "ManageConfiguration";

    // M13 — Audit Committee
    public const string AcMember = "ACMember";
    public const string AcChair = "ACChair";
    public const string ViewAcPacks = "ViewACPacks";
    public const string GenerateAcPack = "GenerateACPack";
    public const string Cia = "CIA";

    // M14 — Integrations
    public const string ViewIntegrations = "ViewIntegrations";
    public const string ConfigureIntegrations = "ConfigureIntegrations";
    public const string ViewIntegrationHealth = "ViewIntegrationHealth";
    public const string ConfigureWebhooks = "ConfigureWebhooks";

    // M15 — Administration
    public const string ViewBankSettings = "ViewBankSettings";
    public const string ManageBankSettings = "ManageBankSettings";
    public const string ViewSystemHealth = "ViewSystemHealth";
    public const string ManageSupportChannel = "ManageSupportChannel";
    public const string ManageRetention = "ManageRetention";
    public const string ExecRestore = "ExecRestore";
    public const string InstallReleases = "InstallReleases";
    public const string ConfigureLimits = "ConfigureLimits";
    public const string AdminOps = "AdminOps";
}
