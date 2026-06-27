using AuditX.Domain.Authorization;
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
