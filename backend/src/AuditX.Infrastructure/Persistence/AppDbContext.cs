using AuditX.Domain.Ac;
using AuditX.Domain.Administration;
using AuditX.Domain.Analytics;
using AuditX.Domain.Audits;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Configuration;
using AuditX.Domain.Evidence;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Identity;
using AuditX.Domain.Integrations;
using AuditX.Domain.Notifications;
using AuditX.Domain.Planning;
using AuditX.Domain.ReferenceData;
using AuditX.Domain.Reports;
using AuditX.Domain.Sanctions;
using AuditX.Domain.Templates;
using AuditX.Domain.Universe;
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

    public DbSet<IntegrationConfiguration> Integrations => Set<IntegrationConfiguration>();

    public DbSet<IntegrationHealthStatus> IntegrationHealth => Set<IntegrationHealthStatus>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<SupportChannelSession> SupportChannelSessions => Set<SupportChannelSession>();

    public DbSet<ReleaseInstall> ReleaseInstalls => Set<ReleaseInstall>();

    public DbSet<RestoreDrill> RestoreDrills => Set<RestoreDrill>();

    public DbSet<ObjectRestoreRequest> ObjectRestoreRequests => Set<ObjectRestoreRequest>();

    public DbSet<AuditableEntity> AuditUniverseEntities => Set<AuditableEntity>();

    public DbSet<RiskDimension> RiskDimensions => Set<RiskDimension>();

    public DbSet<EntityTypeTaxonomy> EntityTypeTaxonomy => Set<EntityTypeTaxonomy>();

    public DbSet<AnnualPlan> AnnualPlans => Set<AnnualPlan>();

    public DbSet<PlanItem> PlanItems => Set<PlanItem>();

    public DbSet<Audit> Audits => Set<Audit>();

    public DbSet<AuditTeamMember> AuditTeamMembers => Set<AuditTeamMember>();

    public DbSet<AuditSection> AuditSections => Set<AuditSection>();

    public DbSet<AuditChecklistItem> AuditChecklistItems => Set<AuditChecklistItem>();

    public DbSet<ChecklistResponse> ChecklistResponses => Set<ChecklistResponse>();

    public DbSet<EvidenceFile> EvidenceFiles => Set<EvidenceFile>();

    public DbSet<AuditException> Exceptions => Set<AuditException>();

    public DbSet<MapAction> MapActions => Set<MapAction>();

    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    public DbSet<NotificationDispatch> NotificationDispatches => Set<NotificationDispatch>();

    public DbSet<SanctionsCase> SanctionsCases => Set<SanctionsCase>();

    public DbSet<SanctionsGridVersion> SanctionsGridVersions => Set<SanctionsGridVersion>();

    public DbSet<SanctionsAppeal> SanctionsAppeals => Set<SanctionsAppeal>();

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();

    public DbSet<Dashboard> Dashboards => Set<Dashboard>();

    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();

    public DbSet<RecurrenceCluster> RecurrenceClusters => Set<RecurrenceCluster>();

    public DbSet<AnalyticsSnapshot> AnalyticsSnapshots => Set<AnalyticsSnapshot>();

    public DbSet<AuditX.Domain.Organization.OrgUnit> OrgUnits => Set<AuditX.Domain.Organization.OrgUnit>();

    public DbSet<BankConfiguration> BankConfigurations => Set<BankConfiguration>();

    public DbSet<ReferenceDataItem> ReferenceDataItems => Set<ReferenceDataItem>();

    public DbSet<AcPack> AcPacks => Set<AcPack>();

    public DbSet<AcPackDistribution> AcPackDistributions => Set<AcPackDistribution>();

    public DbSet<AcActionItem> AcActionItems => Set<AcActionItem>();

    public DbSet<AcComment> AcComments => Set<AcComment>();

    public DbSet<FindingVisibilityRestriction> FindingVisibilityRestrictions => Set<FindingVisibilityRestriction>();

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
