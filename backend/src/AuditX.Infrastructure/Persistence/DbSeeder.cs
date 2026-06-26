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

        if (seedDevelopmentUsers)
        {
            await SeedDevelopmentUsersAsync(rolesByName, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Database seed completed (development users: {Dev}).", seedDevelopmentUsers);
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
