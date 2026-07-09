using AuditX.Domain.Enums;

namespace AuditX.Domain.Authorization;

/// <summary>
/// The authoritative, code-defined catalogue of every permission the platform recognises. Exposed
/// read-only via <c>GET /api/v1/permissions/catalogue</c> (US-M1-014) and used to validate that role
/// definitions reference only known permission keys.
/// </summary>
public static class PermissionCatalogue
{
    private static readonly PermissionDefinition[] Definitions =
    [
        // M1 — Identity, Roles & Permissions
        new(PermissionKeys.ManageUsers, "Manage users", "Create, deactivate, import and assign roles to users.", "M1", PermissionScopeType.Global),
        new(PermissionKeys.ManageRoles, "Manage roles", "Create, edit, archive custom roles and configure inheritance.", "M1", PermissionScopeType.Global),
        new(PermissionKeys.Delegate, "Delegate roles", "Grant another user time-bounded access to a role.", "M1", PermissionScopeType.Global),
        new(PermissionKeys.ViewAuditTrail, "View audit trail", "Query the append-only audit trail.", "M1", PermissionScopeType.Global),
        new(PermissionKeys.ExportAuditTrail, "Export audit trail", "Export audit-trail extracts (PDF/CSV).", "M1", PermissionScopeType.Global),
        new(PermissionKeys.ViewConfig, "View configuration", "Read bank-wide configuration.", "M1", PermissionScopeType.Global),

        // M2 — Templates
        new(PermissionKeys.ViewTemplates, "View templates", "Browse the template library.", "M2", PermissionScopeType.Global),
        new(PermissionKeys.ManageTemplates, "Manage templates", "Author, version, publish, clone and archive templates.", "M2", PermissionScopeType.Global),

        // M3 — Universe & Planning
        new(PermissionKeys.ViewUniverse, "View universe", "Browse auditable entities.", "M3", PermissionScopeType.Global),
        new(PermissionKeys.ManageUniverse, "Manage universe", "Create, update and archive auditable entities.", "M3", PermissionScopeType.Global),
        new(PermissionKeys.ScoreRisk, "Score risk", "Set inherent/residual risk scores on entities.", "M3", PermissionScopeType.UniverseEntity),
        new(PermissionKeys.ViewPlan, "View plan", "View annual audit plans.", "M3", PermissionScopeType.Global),
        new(PermissionKeys.ManagePlan, "Manage plan", "Create and edit annual audit plans.", "M3", PermissionScopeType.Global),
        new(PermissionKeys.ViewCoverage, "View coverage", "View coverage analytics.", "M3", PermissionScopeType.Global),

        // M4 — Audits
        new(PermissionKeys.ViewAudits, "View audits", "List audits within the user's scope.", "M4", PermissionScopeType.Global),
        new(PermissionKeys.ViewAudit, "View audit", "View a specific audit.", "M4", PermissionScopeType.Audit),
        new(PermissionKeys.CreateAudit, "Create audit", "Create new audits.", "M4", PermissionScopeType.AuditType),
        new(PermissionKeys.ManageAudit, "Manage audit", "Manage an audit's team, checklist and lifecycle.", "M4", PermissionScopeType.Audit),

        // M5 — Execution
        new(PermissionKeys.RespondItem, "Respond to items", "Record verdicts and comments on checklist items.", "M5", PermissionScopeType.Audit),
        new(PermissionKeys.UploadEvidence, "Upload evidence", "Attach evidence files to responses/actions.", "M5", PermissionScopeType.Audit),
        new(PermissionKeys.ViewEvidence, "View evidence", "Download and view evidence files.", "M5", PermissionScopeType.Audit),
        new(PermissionKeys.ManageEvidence, "Manage evidence", "Soft-delete evidence with reason.", "M5", PermissionScopeType.Audit),

        // Time tracking (P0-B)
        new(PermissionKeys.LogTime, "Log time", "Record time/effort against an audit.", "M5", PermissionScopeType.Audit),
        new(PermissionKeys.ViewTimeEntries, "View time entries", "View logged time, budget-vs-actual and utilisation.", "M5", PermissionScopeType.Global),

        // Risk register (P1-A)
        new(PermissionKeys.ViewRisk, "View risks", "Browse the enterprise risk register and heatmap.", "M3", PermissionScopeType.Global),
        new(PermissionKeys.ManageRisk, "Manage risks", "Register, assess, treat and close enterprise risks.", "M3", PermissionScopeType.Global),

        // M6 — Exceptions
        new(PermissionKeys.ViewExceptions, "View exceptions", "Browse the cross-audit exception tracker.", "M6", PermissionScopeType.Global),
        new(PermissionKeys.RaiseException, "Raise exception", "Raise an exception from a failed item.", "M6", PermissionScopeType.Audit),
        new(PermissionKeys.ManageException, "Manage exception", "Edit severity, owner and dates of an exception.", "M6", PermissionScopeType.Audit),
        new(PermissionKeys.SubmitMap, "Submit MAP", "Submit a Management Action Plan.", "M6", PermissionScopeType.Audit),
        new(PermissionKeys.ApproveMap, "Approve MAP", "Approve or reject a Management Action Plan.", "M6", PermissionScopeType.Audit),
        new(PermissionKeys.CloseException, "Close exception", "Verify evidence and close an exception.", "M6", PermissionScopeType.Audit),
        new(PermissionKeys.CancelException, "Cancel exception", "Cancel an exception with reason.", "M6", PermissionScopeType.Audit),
        new(PermissionKeys.ConfigureExceptionWorkflow, "Configure exception workflow", "Edit the exception state machine and escalation rules.", "M6", PermissionScopeType.Global),

        // M7 — Sanctions
        new(PermissionKeys.ViewSanctions, "View sanctions", "View sanctions cases (subject masked unless on case team).", "M7", PermissionScopeType.Global),
        new(PermissionKeys.TriggerSanctions, "Trigger sanctions", "Open a sanctions case from an exception.", "M7", PermissionScopeType.Audit),
        new(PermissionKeys.RecommendSanction, "Recommend sanction", "Record a sanction recommendation against the grid.", "M7", PermissionScopeType.Relational),
        new(PermissionKeys.RecordHrOutcome, "Record HR outcome", "Record the HR disciplinary outcome.", "M7", PermissionScopeType.Relational),
        new(PermissionKeys.ReferToDc, "Refer to DC", "Refer a case to the Disciplinary Committee.", "M7", PermissionScopeType.Relational),
        new(PermissionKeys.DcMember, "Disciplinary Committee member", "Deliberate and record DC decisions.", "M7", PermissionScopeType.Global),
        new(PermissionKeys.FileAppeal, "File appeal", "File an appeal against a sanctions decision.", "M7", PermissionScopeType.Relational),
        new(PermissionKeys.DecideAppeal, "Decide appeal", "Record an appeal decision.", "M7", PermissionScopeType.Relational),
        new(PermissionKeys.ManageGrid, "Manage sanctions grid", "Author and activate sanctions-grid versions.", "M7", PermissionScopeType.Global),
        new(PermissionKeys.ViewGrid, "View sanctions grid", "View the active sanctions grid.", "M7", PermissionScopeType.Global),

        // M8 — Reports
        new(PermissionKeys.GenerateReport, "Generate report", "Generate an audit report.", "M8", PermissionScopeType.Audit),
        new(PermissionKeys.ViewReport, "View report", "View/download a generated report.", "M8", PermissionScopeType.Audit),
        new(PermissionKeys.DistributeReport, "Distribute report", "Distribute a report to recipients.", "M8", PermissionScopeType.Audit),
        new(PermissionKeys.ConfigureReports, "Configure reports", "Manage report templates.", "M8", PermissionScopeType.Global),

        // M9 — Analytics
        new(PermissionKeys.ViewAnalytics, "View analytics", "View dashboards and analytics.", "M9", PermissionScopeType.Global),
        new(PermissionKeys.AdHocQueryUse, "Ad-hoc query", "Run constrained ad-hoc analytics queries.", "M9", PermissionScopeType.Global),
        new(PermissionKeys.ConfigureDashboards, "Configure dashboards", "Add/remove/arrange dashboard widgets.", "M9", PermissionScopeType.Global),
        new(PermissionKeys.ConfigurePredictive, "Configure predictive indicators", "Define predictive indicator rules.", "M9", PermissionScopeType.Global),
        new(PermissionKeys.PerformanceAnalyticsView, "View performance scorecards", "View function-performance scorecards (CIA by default).", "M9", PermissionScopeType.Global),
        new(PermissionKeys.SensitiveQueryAccess, "Sensitive query access", "Run analytics queries that may surface sensitive data (catalogued; ungranted until the ad-hoc slice ships).", "M9", PermissionScopeType.Global),

        // M10 — Notifications
        new(PermissionKeys.ConfigureNotifications, "Configure notifications", "Manage notification rules and templates.", "M10", PermissionScopeType.Global),

        // M12 — Configuration
        new(PermissionKeys.ManageConfiguration, "Manage configuration", "Version, activate and roll back bank-wide configuration.", "M12", PermissionScopeType.Global),

        // M13 — Audit Committee
        new(PermissionKeys.AcMember, "Audit Committee member", "Access the AC workspace and dashboard.", "M13", PermissionScopeType.Global),
        new(PermissionKeys.AcChair, "Audit Committee chair", "Record AC plan decisions and acknowledge closures.", "M13", PermissionScopeType.Global),
        new(PermissionKeys.ViewAcPacks, "View AC packs", "View distributed Audit Committee packs.", "M13", PermissionScopeType.Global),
        new(PermissionKeys.GenerateAcPack, "Generate AC pack", "Generate Audit Committee packs.", "M13", PermissionScopeType.Global),
        new(PermissionKeys.Cia, "Chief Internal Auditor", "CIA oversight powers (countersign, AC approval, restricted visibility).", "M13", PermissionScopeType.Global),

        // M14 — Integrations
        new(PermissionKeys.ViewIntegrations, "View integrations", "View configured integrations.", "M14", PermissionScopeType.Global),
        new(PermissionKeys.ConfigureIntegrations, "Configure integrations", "Configure AD/SMTP/SMS/storage/SIEM integrations.", "M14", PermissionScopeType.Global),
        new(PermissionKeys.ViewIntegrationHealth, "View integration health", "View integration health and metrics.", "M14", PermissionScopeType.Global),
        new(PermissionKeys.ConfigureWebhooks, "Configure webhooks", "Manage outbound webhook subscriptions.", "M14", PermissionScopeType.Global),

        // M15 — Administration
        new(PermissionKeys.ViewBankSettings, "View bank settings", "Read deployment-wide settings.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.ManageBankSettings, "Manage bank settings", "Edit deployment-wide settings.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.ViewSystemHealth, "View system health", "View platform health and capacity surfaces.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.ManageSupportChannel, "Manage support channel", "Enable/revoke the ITANDT support channel.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.ManageRetention, "Manage retention", "Configure retention policies and extensions.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.ExecRestore, "Execute restore", "Execute per-object restore operations.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.InstallReleases, "Install releases", "Install approved (signed) release packages.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.ConfigureLimits, "Configure limits", "Adjust bank-wide resource limits.", "M15", PermissionScopeType.Global),
        new(PermissionKeys.AdminOps, "Operations administration", "Operational surfaces (dead-letter queues, tokens, drills).", "M15", PermissionScopeType.Global),
    ];

    /// <summary>All catalogue entries, ordered by module then key.</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } =
        Definitions.OrderBy(d => d.Module).ThenBy(d => d.Key).ToArray();

    private static readonly HashSet<string> KeySet = Definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>True if <paramref name="key"/> is a recognised permission key.</summary>
    public static bool IsValidKey(string key) => KeySet.Contains(key);
}
