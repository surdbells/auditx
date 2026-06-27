using AuditX.Application.Sanctions;
using AuditX.Domain.Authorization;
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
        await SeedSanctionsGridAsync(cancellationToken);
        await SeedNotificationDefaultsAsync(cancellationToken);

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
