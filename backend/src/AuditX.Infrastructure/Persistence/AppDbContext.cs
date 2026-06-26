using AuditX.Domain.AuditTrail;
using AuditX.Domain.Identity;
using AuditX.Domain.Templates;
using AuditX.Infrastructure.Persistence.Naming;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence;

/// <summary>
/// The EF Core database context for AuditX. Single-tenant: there is no tenant discriminator. Table and
/// column names are mapped to snake_case to match the specification and SQL conventions.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<MakerCheckerAction> MakerCheckerActions => Set<MakerCheckerAction>();

    public DbSet<MakerCheckerGate> MakerCheckerGates => Set<MakerCheckerGate>();

    public DbSet<BankSettings> BankSettings => Set<BankSettings>();

    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<TemplateItem> TemplateItems => Set<TemplateItem>();

    public DbSet<TemplateSection> TemplateSections => Set<TemplateSection>();

    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Map all column names to snake_case for a consistent SQL schema.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(SnakeCase.Convert(property.Name));
            }
        }
    }
}
