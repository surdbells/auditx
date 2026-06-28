using AuditX.Application.Ac;
using AuditX.Application.Configuration;
using AuditX.Application.Sanctions;
using AuditX.Domain.Authorization;
using AuditX.Domain.Configuration;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using AuditX.Infrastructure.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Persistence;

/// <summary>
/// Idempotent deployment seed: bank settings, the four built-in roles with their v2.0 permission sets,
/// the default maker-checker gates, and — only when the development identity provider is active — the
/// seeded development users (with the admin bootstrapped into the Administrator role).
/// </summary>
public sealed class DbSeeder(AppDbContext db, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(bool seedDevelopmentUsers, CancellationToken cancellationToken = default)
    {
        await SeedBankSettingsAsync(cancellationToken);
        var rolesByName = await SeedBuiltInRolesAsync(cancellationToken);
        await SeedMakerCheckerGatesAsync(cancellationToken);
        await SeedRiskDimensionsAsync(cancellationToken);
        await SeedEntityTypesAsync(cancellationToken);
        await SeedSanctionsRolesAsync(cancellationToken);
        await SeedAcRolesAsync(cancellationToken);
        await SeedSanctionsGridAsync(cancellationToken);
        await SeedReportTemplateAsync(cancellationToken);
        await SeedNotificationDefaultsAsync(cancellationToken);
        await SeedDashboardsAsync(cancellationToken);
        await SeedExceptionDefaultsConfigurationAsync(cancellationToken);

        if (seedDevelopmentUsers)
        {
            await SeedDevelopmentUsersAsync(rolesByName, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Database seed completed (development users: {Dev}).", seedDevelopmentUsers);
    }

    private async Task SeedNotificationDefaultsAsync(CancellationToken cancellationToken)
    {
        // Guard each template and rule independently (not a single table-level AnyAsync) so a partial state —
        // e.g. rules removed while System templates remain — re-seeds cleanly instead of hitting the unique index.
        var existingTemplateKeys = await db.NotificationTemplates
            .Where(t => t.Scope == Domain.Enums.TemplateScope.System && t.Channel == Domain.Enums.NotificationChannel.Email)
            .Select(t => t.TemplateKey)
            .ToListAsync(cancellationToken);
        var existingDefaultRuleEvents = await db.NotificationRules
            .Where(r => r.IsSystemDefault)
            .Select(r => r.EventType)
            .ToListAsync(cancellationToken);

        // (eventType, templateKey, recipientType, recipientValue, channels, subject, body). Recipients are
        // mostly payload-derived (the event carries the target user id); map_submitted notifies the role.
        var defaults = new (string EventType, string TemplateKey, string RecipientType, string RecipientValue, string Channels, string Subject, string Body)[]
        {
            ("audit_team_member_added", "team_assignment", "payload_derived", "UserId", "[\"email\"]",
                "You have been added to an audit", "You have been added to audit {{ AuditId }} as {{ Role }}."),
            ("audit_lead_transferred", "audit_lead_transferred", "payload_derived", "IncomingLeadUserId", "[\"email\"]",
                "You are now the audit lead", "You are now the lead for audit {{ AuditId }}."),
            ("item_assigned", "item_assigned", "payload_derived", "AssigneeUserId", "[\"email\"]",
                "A checklist item was assigned to you", "Checklist item {{ ItemId }} on audit {{ AuditId }} has been assigned to you."),
            ("exception_raised", "exception_raised", "payload_derived", "OwnerUserId", "[\"email\"]",
                "An exception was raised against you", "Exception {{ ExceptionId }} ({{ Severity }}) was raised on audit {{ AuditId }}. Please review."),
            ("exception_owner_reassigned", "exception_owner_reassigned", "payload_derived", "NewOwnerUserId", "[\"email\"]",
                "An exception was reassigned to you", "Exception {{ ExceptionId }} has been reassigned to you."),
            ("map_submitted", "map_submitted", "role", BuiltInRoles.AuditManagerName, "[\"email\"]",
                "A management action plan was submitted", "A MAP was submitted for exception {{ ExceptionId }} and awaits your review."),

            // M7 sanctions (pinned event-key/recipient-field names). Multi-party events resolve a single ROLE recipient
            // set (payload_derived resolves only ONE user, so it is reserved for the appeal-routing event).
            (Domain.AuditTrail.AuditEventTypes.SanctionsRecommended, "sanctions_recommended", "role", SanctionsRoles.HrRepresentative, "[\"email\"]",
                "A sanction has been recommended", "A sanction has been recommended for sanctions case {{ SanctionsCaseId }}. Please review."),
            (Domain.AuditTrail.AuditEventTypes.HrOutcomeRecorded, "hr_outcome_recorded", "role", BuiltInRoles.AuditManagerName, "[\"email\"]",
                "An HR outcome was recorded", "An HR outcome ({{ outcomeType }}) was recorded for sanctions case {{ SanctionsCaseId }}."),
            (Domain.AuditTrail.AuditEventTypes.DcReferral, "dc_referral", "role", SanctionsRoles.DisciplinaryCommitteeMember, "[\"email\"]",
                "A case was referred to the disciplinary committee", "Sanctions case {{ SanctionsCaseId }} has been referred to the disciplinary committee."),
            (Domain.AuditTrail.AuditEventTypes.DcDecisionRecorded, "dc_decision_recorded", "role", BuiltInRoles.AuditManagerName, "[\"email\"]",
                "A disciplinary decision was recorded", "A disciplinary decision ({{ decision }}) was recorded for sanctions case {{ SanctionsCaseId }}."),
            (Domain.AuditTrail.AuditEventTypes.AppealFiled, "appeal_filed", "payload_derived", "RoutedToUserId", "[\"email\"]",
                "An appeal has been filed", "An appeal has been filed for sanctions case {{ SanctionsCaseId }} and routed to you for decision."),
            (Domain.AuditTrail.AuditEventTypes.AppealOutcomeRecorded, "appeal_outcome_recorded", "payload_derived", "AppellantUserId", "[\"email\"]",
                "Your appeal has been decided", "Your appeal for sanctions case {{ SanctionsCaseId }} has been decided ({{ outcome }})."),

            // M8 reports. report_generated → the requesting Audit Manager (the single user on GeneratedBy).
            (Domain.AuditTrail.AuditEventTypes.ReportGenerated, "report_generated", "payload_derived", "GeneratedBy", "[\"email\"]",
                "Your audit report is ready", "Report version {{ VersionNumber }} for audit {{ AuditId }} has been generated and is ready to view."),
            // report_hash_mismatch → Security/Administrator. The event payload carries Severity=Critical so the M10
            // suppression-override + SMS escalation fires (channel data-driven via the rule; defaults to email).
            (Domain.AuditTrail.AuditEventTypes.ReportHashMismatch, "report_integrity_alert", "role", BuiltInRoles.AdministratorName, "[\"email\"]",
                "Report integrity alert", "Report {{ ReportId }} (audit {{ AuditId }}) failed SHA-256 verification. Expected {{ ExpectedHash }}, recomputed {{ RecomputedHash }}. Investigate immediately."),

            // M9 analytics. recurrence_cluster_detected → the Audit Manager role (G6). Raised by the daily scan job
            // when a (entity, category) group newly reaches the closed-exception recurrence threshold.
            (Domain.AuditTrail.AuditEventTypes.RecurrenceClusterDetected, "recurrence_cluster_detected", "role", BuiltInRoles.AuditManagerName, "[\"email\"]",
                "A recurring control weakness was detected", "{{ ClosedExceptionCount }} closed exceptions for entity {{ AuditableEntityId }} (category {{ Category }}) within {{ WindowMonths }} months indicate a recurrence. Please review."),

            // M3 planning AC routing (A4/D1). plan_submitted → the AC Member role (a plan awaits the committee's
            // attention); plan_decided → the CIA (route on the single PlanDecidedEvent's Decision payload).
            (Domain.AuditTrail.AuditEventTypes.PlanSubmitted, "plan_submitted", "role", AcRoles.AuditCommitteeMember, "[\"email\"]",
                "An annual audit plan was submitted", "Annual plan {{ PlanId }} ({{ PeriodLabel }}) has been submitted and awaits audit-committee review."),
            ("plan_decided", "plan_decided", "role", AcRoles.ChiefInternalAuditor, "[\"email\"]",
                "An annual audit plan decision was recorded", "A decision ({{ Decision }}) was recorded on annual plan {{ PlanId }}."),

            // M13 audit committee. CIA-role recipients for generated/comment/action-item/ack; per-recipient
            // payload_derived for distributed (one AcPackDistributedEvent carries one RecipientUserId).
            (Domain.AuditTrail.AuditEventTypes.AcPackGenerated, "ac_pack_generated", "role", AcRoles.ChiefInternalAuditor, "[\"email\"]",
                "An audit-committee pack is ready for review", "AC pack version {{ VersionNumber }} has been generated and awaits your review and approval."),
            (Domain.AuditTrail.AuditEventTypes.AcPackDistributed, "ac_pack_distributed", "payload_derived", "RecipientUserId", "[\"email\"]",
                "An audit-committee pack has been distributed", "Audit-committee pack version {{ VersionNumber }} ({{ AcMeetingLabel }}) has been distributed to you."),
            (Domain.AuditTrail.AuditEventTypes.AcActionItemCreated, "ac_action_item_created", "role", AcRoles.ChiefInternalAuditor, "[\"email\"]",
                "A new audit-committee action item was raised", "Audit-committee action item \"{{ Title }}\" has been raised and assigned for follow-up."),
            (Domain.AuditTrail.AuditEventTypes.AcActionItemClosureAcknowledged, "ac_action_item_closure_acknowledged", "role", AcRoles.ChiefInternalAuditor, "[\"email\"]",
                "An audit-committee action item closure was acknowledged", "The audit committee chair has acknowledged the closure of action item {{ AcActionItemId }}."),
            (Domain.AuditTrail.AuditEventTypes.AcCommentAdded, "ac_comment_added", "role", AcRoles.ChiefInternalAuditor, "[\"email\"]",
                "A new audit-committee comment was posted", "A comment was posted on {{ TargetType }} {{ TargetId }} by the audit committee."),
        };

        foreach (var d in defaults)
        {
            if (!existingTemplateKeys.Contains(d.TemplateKey))
            {
                db.NotificationTemplates.Add(Domain.Notifications.NotificationTemplate.Create(
                    d.TemplateKey, Domain.Enums.NotificationChannel.Email, Domain.Enums.TemplateScope.System, d.Subject, d.Body));
            }

            if (!existingDefaultRuleEvents.Contains(d.EventType))
            {
                var recipientJson = $"{{\"type\":\"{d.RecipientType}\",\"value\":\"{d.RecipientValue}\"}}";
                db.NotificationRules.Add(Domain.Notifications.NotificationRule.Create(
                    d.EventType, $"Default: {d.EventType}", recipientJson, d.Channels, d.TemplateKey, isActive: true, isSystemDefault: true));
            }
        }
    }

    /// <summary>
    /// Seed the six system dashboards (M9) with sensible bank-wide default widgets. Idempotent per slug. The
    /// sanctions-consistency dashboard requires the CIA permission; the function-performance dashboard carries the
    /// performance-scorecards widget and is therefore gated by PerformanceAnalyticsView. The other four are
    /// ungated (visible to anyone authenticated; their data endpoints are still ViewAnalytics-gated).
    /// </summary>
    private async Task SeedDashboardsAsync(CancellationToken cancellationToken)
    {
        var existingSlugs = await db.Dashboards.IgnoreQueryFilters().Select(d => d.Slug).ToListAsync(cancellationToken);

        // (slug, name, description, permissionRequired, widgets[(type, metricKey, title, position)]).
        var dashboards = new (string Slug, string Name, string? Description, string? Permission, (WidgetType Type, string Metric, string Title)[] Widgets)[]
        {
            ("function_performance", "Function performance",
                "Plan execution, in-flight work, exception throughput and per-lead scorecards.",
                PermissionKeys.PerformanceAnalyticsView,
                [
                    (WidgetType.SingleMetric, Application.Analytics.Queries.DashboardMetricKeys.FunctionPerformance, "Function performance"),
                    (WidgetType.Table, Application.Analytics.Queries.DashboardMetricKeys.PerformanceScorecards, "Audit-lead scorecards"),
                ]),
            ("exception_portfolio", "Exception portfolio",
                "Open exceptions by severity, age and entity, with closure-time trends.",
                null,
                [
                    (WidgetType.Chart, Application.Analytics.Queries.DashboardMetricKeys.ExceptionPortfolio, "Exception portfolio"),
                ]),
            ("coverage", "Audit coverage",
                "Coverage matrix of completed audits across the universe.",
                null,
                [
                    (WidgetType.Table, Application.Analytics.Queries.DashboardMetricKeys.Coverage, "Coverage matrix"),
                ]),
            ("sanctions_consistency", "Sanctions consistency",
                "Grid adherence, deviation and appeal rates by business unit (subject identity omitted).",
                PermissionKeys.Cia,
                [
                    (WidgetType.Table, Application.Analytics.Queries.DashboardMetricKeys.SanctionsConsistency, "Sanctions consistency"),
                ]),
            ("recurrence_cluster", "Recurrence clusters",
                "Detected recurring control weaknesses (≥3 closed exceptions per entity/category).",
                null,
                [
                    (WidgetType.Table, Application.Analytics.Queries.DashboardMetricKeys.RecurrenceClusters, "Recurrence clusters"),
                ]),
            ("audit_committee", "Audit committee",
                "Material findings and plan status for the audit-committee surface.",
                null,
                [
                    (WidgetType.Table, Application.Analytics.Queries.DashboardMetricKeys.MaterialFindings, "Material findings"),
                    (WidgetType.SingleMetric, Application.Analytics.Queries.DashboardMetricKeys.PlanStatus, "Plan status"),
                ]),
        };

        foreach (var d in dashboards)
        {
            if (existingSlugs.Contains(d.Slug, StringComparer.Ordinal))
            {
                continue;
            }

            var dashboard = Domain.Analytics.Dashboard.Create(d.Slug, d.Name, d.Description, d.Permission, isSystemDefault: true);
            var position = 0;
            foreach (var w in d.Widgets)
            {
                dashboard.AddWidget(w.Type, w.Metric, w.Title, targetRoleId: null, position: position++, configJson: null);
            }

            db.Dashboards.Add(dashboard);
        }
    }

    private async Task SeedRiskDimensionsAsync(CancellationToken cancellationToken)
    {
        if (await db.RiskDimensions.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var name in new[] { "Financial", "Operational", "Regulatory", "Reputational" })
        {
            db.RiskDimensions.Add(Domain.Universe.RiskDimension.Create(name, weight: 1.00m, scaleMin: 1, scaleMax: 5, scaleLabelOverridesJson: null));
        }
    }

    private async Task SeedEntityTypesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.EntityTypeTaxonomy.Select(t => t.Name).ToListAsync(cancellationToken);
        foreach (var name in new[] { "branch", "process", "system", "vendor", "product" })
        {
            if (!existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                db.EntityTypeTaxonomy.Add(Domain.Universe.EntityTypeTaxonomy.Create(name));
            }
        }
    }

    /// <summary>
    /// Seed the three non-builtin, bank-reconfigurable M7 cohort roles. The cohort permissions (RecordHROutcome,
    /// ReferToDC, DCMember, DecideAppeal) map to assignable roles, not to the four immutable built-ins.
    /// </summary>
    private async Task SeedSanctionsRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Roles.Select(r => r.Name).ToListAsync(cancellationToken);

        var roleDefs = new (string Name, string Description, string[] Permissions)[]
        {
            (SanctionsRoles.HrRepresentative, "Records HR outcomes on recommended sanctions and refers contested cases to the disciplinary committee.",
                [PermissionKeys.RecordHrOutcome, PermissionKeys.ReferToDc, PermissionKeys.ViewSanctions]),
            (SanctionsRoles.DisciplinaryCommitteeMember, "Deliberates and records disciplinary committee decisions on referred sanctions cases.",
                [PermissionKeys.DcMember, PermissionKeys.ViewSanctions]),
            (SanctionsRoles.AppealsAuthority, "Decides appeals filed against sanctions decisions.",
                [PermissionKeys.DecideAppeal, PermissionKeys.ViewSanctions]),
        };

        foreach (var (name, description, permissions) in roleDefs)
        {
            if (existing.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            var role = Role.CreateCustom(name, description);
            role.SetPermissions(permissions.Select(key => (key, PermissionScopeType.Global, (string?)null)));
            db.Roles.Add(role);
        }
    }

    /// <summary>
    /// Seed the three non-builtin, least-privilege M13 audit-committee / CIA roles (B2, FR-M1-011). Custom roles
    /// (<c>is_builtin=false</c>) so a bank can reconfigure them. CIA stays on the Administrator built-in too; the
    /// dedicated "Chief Internal Auditor" role makes the CIA notifications target a sensible recipient set.
    /// </summary>
    private async Task SeedAcRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Roles.Select(r => r.Name).ToListAsync(cancellationToken);

        var roleDefs = new (string Name, string Description, string[] Permissions)[]
        {
            // ViewPlan is the least-privilege grant that lets the AC read the submitted-plans list (D4) — the existing
            // GET /annual-plans gate — without widening that gate for everyone.
            (AcRoles.AuditCommitteeMember, "Reads approved/distributed audit-committee packs, the AC dashboard, action items and submitted plans.",
                [PermissionKeys.AcMember, PermissionKeys.ViewAcPacks, PermissionKeys.ViewPlan]),
            (AcRoles.AuditCommitteeChair, "An audit-committee member who also chairs the committee: decides plans and acknowledges action-item closures.",
                [PermissionKeys.AcMember, PermissionKeys.ViewAcPacks, PermissionKeys.ViewPlan, PermissionKeys.AcChair]),
            (AcRoles.ChiefInternalAuditor, "Generates, reviews, approves and distributes audit-committee packs and manages restricted-finding visibility.",
                [PermissionKeys.Cia, PermissionKeys.GenerateAcPack, PermissionKeys.ViewAcPacks, PermissionKeys.ViewPlan]),
        };

        foreach (var (name, description, permissions) in roleDefs)
        {
            if (existing.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            var role = Role.CreateCustom(name, description);
            role.SetPermissions(permissions.Select(key => (key, PermissionScopeType.Global, (string?)null)));
            db.Roles.Add(role);
        }
    }

    /// <summary>
    /// Seed the active v1 <c>exception_defaults</c> configuration (M12) reproducing the pre-M12 hardcoded values
    /// EXACTLY (Critical 14 / High 30 / Medium 45 / Low 60, recurrence window 24 months, threshold 3). This is the
    /// critical regression guard: the upgrade must reproduce current behaviour. Idempotent (guarded per-domain).
    /// </summary>
    private async Task SeedExceptionDefaultsConfigurationAsync(CancellationToken cancellationToken)
    {
        if (await db.BankConfigurations.AnyAsync(c => c.Domain == ConfigurationDomains.ExceptionDefaults, cancellationToken))
        {
            return;
        }

        var definitionJson = ConfigurationDefinitions.SerializeExceptionDefaults(ExceptionDefaultsDefinition.HardcodedFallback);
        var config = BankConfiguration.CreateDraft(
            ConfigurationDomains.ExceptionDefaults, versionNumber: 1, definitionJson,
            changeReason: "Initial exception defaults seeded on deployment (reproduces pre-M12 hardcoded values).",
            createdBy: Guid.Empty, nowUtc: DateTimeOffset.UtcNow);
        config.Activate(activatedBy: Guid.Empty, nowUtc: DateTimeOffset.UtcNow);
        db.BankConfigurations.Add(config);
    }

    /// <summary>Seed one active default sanctions grid (version 1) with a few representative cells per the E3 schema.</summary>
    private async Task SeedSanctionsGridAsync(CancellationToken cancellationToken)
    {
        if (await db.SanctionsGridVersions.AnyAsync(cancellationToken))
        {
            return;
        }

        // Cell key: "<category>|<severity>|<recurrence true|false>" → { recommended_range }.
        const string gridJson =
            "{\"cells\":{" +
            "\"cash_handling|critical|false\":{\"recommended_range\":\"Final written warning to dismissal\"}," +
            "\"cash_handling|critical|true\":{\"recommended_range\":\"Dismissal\"}," +
            "\"cash_handling|high|false\":{\"recommended_range\":\"Written warning to two-week suspension\"}," +
            "\"process_breach|medium|false\":{\"recommended_range\":\"Verbal warning to written warning\"}," +
            "\"process_breach|low|false\":{\"recommended_range\":\"Coaching to verbal warning\"}" +
            "}}";

        var grid = Domain.Sanctions.SanctionsGridVersion.CreateDraft(1, gridJson, createdBy: Guid.Empty, nowUtc: DateTimeOffset.UtcNow);
        grid.Activate("Initial bank sanctions grid seeded on deployment.", activatedBy: Guid.Empty, nowUtc: DateTimeOffset.UtcNow);
        db.SanctionsGridVersions.Add(grid);
    }

    /// <summary>Seed one active default report template (version 1) with the standard section set (US-M8-007). Idempotent.</summary>
    private async Task SeedReportTemplateAsync(CancellationToken cancellationToken)
    {
        if (await db.ReportTemplates.AnyAsync(cancellationToken))
        {
            return;
        }

        // sections[] — each section has a key, a title and an optional FIXED condition flag (evaluated against the
        // composition's flag bag; unknown flags omit the section). The conditional findings/evidence sections use
        // the three known flags has_critical_exceptions / has_evidence_files / has_recurrence_flags.
        const string definitionJson =
            "{\"title\":\"Audit Report\",\"sections\":[" +
            "{\"key\":\"executive_summary\",\"title\":\"Executive summary\"}," +
            "{\"key\":\"scope\",\"title\":\"Scope\"}," +
            "{\"key\":\"methodology\",\"title\":\"Methodology\"}," +
            "{\"key\":\"findings\",\"title\":\"Findings\"}," +
            "{\"key\":\"evidence\",\"title\":\"Evidence references\",\"condition\":\"has_evidence_files\"}," +
            "{\"key\":\"conclusion\",\"title\":\"Conclusion\"}" +
            "]}";

        var template = Domain.Reports.ReportTemplate.CreateVersion(
            "Default audit report", definitionJson, versionNumber: 1, createdBy: Guid.Empty, nowUtc: DateTimeOffset.UtcNow);
        template.Activate("Default report template seeded on deployment for bank-wide audit reporting.", activatedBy: Guid.Empty, nowUtc: DateTimeOffset.UtcNow);
        db.ReportTemplates.Add(template);
    }

    private async Task SeedBankSettingsAsync(CancellationToken cancellationToken)
    {
        if (!await db.BankSettings.AnyAsync(cancellationToken))
        {
            db.BankSettings.Add(BankSettings.CreateDefault("AuditX"));
        }
    }

    private async Task<Dictionary<string, Role>> SeedBuiltInRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Roles.Include(r => r.Permissions).ToListAsync(cancellationToken);
        var byName = existing.ToDictionary(r => r.Name, StringComparer.Ordinal);

        foreach (var definition in BuiltInRoles.All)
        {
            if (!byName.ContainsKey(definition.Name))
            {
                var role = Role.CreateBuiltIn(definition);
                db.Roles.Add(role);
                byName[role.Name] = role;
            }
        }

        return byName;
    }

    private async Task SeedMakerCheckerGatesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.MakerCheckerGates.Select(g => g.ActionType).ToListAsync(cancellationToken);
        foreach (var actionType in MakerCheckerActionTypes.DefaultEnabled)
        {
            if (!existing.Contains(actionType))
            {
                db.MakerCheckerGates.Add(MakerCheckerGate.Create(actionType, isEnabled: true));
            }
        }
    }

    private async Task SeedDevelopmentUsersAsync(IReadOnlyDictionary<string, Role> rolesByName, CancellationToken cancellationToken)
    {
        foreach (var devUser in DevUsers.All)
        {
            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.AdObjectSid == devUser.ObjectSid, cancellationToken))
            {
                continue;
            }

            var user = User.ProvisionFromDirectory(
                devUser.SamAccountName, devUser.UserPrincipalName, devUser.ObjectSid,
                devUser.Email, devUser.FirstName, devUser.LastName, $"{devUser.FirstName} {devUser.LastName}");
            db.Users.Add(user);

            if (devUser.SeedRoleName is { } roleName && rolesByName.TryGetValue(roleName, out var role))
            {
                db.UserRoles.Add(UserRole.Grant(user.Id, role.Id));
                user.MarkActiveOnFirstRole();
            }
        }
    }
}
