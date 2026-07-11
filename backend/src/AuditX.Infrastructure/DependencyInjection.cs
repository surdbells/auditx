using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Administration;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Integrations;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Infrastructure.Administration;
using AuditX.Infrastructure.Authorization;
using AuditX.Infrastructure.Identity;
using AuditX.Infrastructure.Integrations;
using AuditX.Infrastructure.Messaging;
using AuditX.Infrastructure.Options;
using AuditX.Infrastructure.Persistence;
using AuditX.Infrastructure.Persistence.Interceptors;
using AuditX.Infrastructure.Persistence.Repositories;
using AuditX.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AuditX.Infrastructure;

/// <summary>Registers the infrastructure layer: persistence, identity providers, caching, and adapters.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<IdentityOptions>(configuration.GetSection(IdentityOptions.SectionName));
        services.Configure<ActiveDirectoryOptions>(configuration.GetSection(ActiveDirectoryOptions.SectionName));
        services.Configure<ActiveDirectoryApiOptions>(configuration.GetSection(ActiveDirectoryApiOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var identityOptions = configuration.GetSection(IdentityOptions.SectionName).Get<IdentityOptions>() ?? new IdentityOptions();
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        // Persistence
        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IMakerCheckerRepository, MakerCheckerRepository>();
        services.AddScoped<IMakerCheckerGateRepository, MakerCheckerGateRepository>();
        services.AddScoped<IBankSettingsRepository, BankSettingsRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IOrgUnitRepository, OrgUnitRepository>();
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        services.AddScoped<IAuditUniverseRepository, AuditUniverseRepository>();
        services.AddScoped<IRiskDimensionRepository, RiskDimensionRepository>();
        services.AddScoped<IEntityTypeTaxonomyRepository, EntityTypeTaxonomyRepository>();
        services.AddScoped<IAnnualPlanRepository, AnnualPlanRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IEvidenceRepository, EvidenceRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<IAuditProcedureRepository, AuditProcedureRepository>();
        services.AddScoped<IEvidenceRequestRepository, EvidenceRequestRepository>();
        services.AddScoped<ISavedViewRepository, SavedViewRepository>();
        services.AddScoped<ISharedLinkRepository, SharedLinkRepository>();
        services.AddScoped<IReportScheduleRepository, ReportScheduleRepository>();
        services.AddScoped<IRiskRepository, RiskRepository>();
        services.AddScoped<IControlRepository, ControlRepository>();
        services.AddScoped<IRegulationRepository, RegulationRepository>();
        services.AddScoped<IFindingLinkRepository, FindingLinkRepository>();
        services.AddScoped<IExceptionRepository, ExceptionRepository>();
        services.AddScoped<ISanctionsCaseRepository, SanctionsCaseRepository>();
        services.AddScoped<ISanctionsGridRepository, SanctionsGridRepository>();
        services.AddScoped<ISanctionsAppealRepository, SanctionsAppealRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportTemplateRepository, ReportTemplateRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IRecurrenceClusterRepository, RecurrenceClusterRepository>();
        services.AddScoped<IBankConfigurationRepository, BankConfigurationRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<IAcPackRepository, AcPackRepository>();
        services.AddScoped<IAcActionItemRepository, AcActionItemRepository>();
        services.AddScoped<IAcCommentRepository, AcCommentRepository>();
        services.AddScoped<IFindingVisibilityRestrictionRepository, FindingVisibilityRestrictionRepository>();
        services.AddScoped<Application.Abstractions.Sanctions.IDossierGenerator, Sanctions.HtmlDossierGenerator>();

        // M8 reports: assembler + generation service + format-dispatching renderer (HTML always + DOCX via OpenXml).
        // The concrete renderers are registered under their OWN types and the composite is the sole IReportRenderer,
        // built from them explicitly — registering the composite into the IReportRenderer set it consumes would make
        // IEnumerable<IReportRenderer> circular and fail resolution of the whole generation path.
        services.AddScoped<Application.Reports.Generation.ReportContentAssembler>();
        services.AddScoped<Application.Reports.Generation.StandaloneReportAssembler>();
        services.AddScoped<Application.Reports.Generation.ReportGenerationService>();
        services.AddScoped<Application.Reports.Retention.ReportRetentionService>();
        services.AddScoped<Application.Scheduling.ScheduledReportRunner>();
        services.AddScoped<Reports.HtmlReportRenderer>();
        services.AddScoped<Reports.OpenXmlReportRenderer>();
        services.AddScoped<Reports.PdfReportRenderer>();
        services.AddScoped<Reports.CsvReportRenderer>();
        services.AddScoped<Reports.XlsxReportRenderer>();
        services.AddScoped<Application.Abstractions.Reports.IReportRenderer>(sp => new Reports.CompositeReportRenderer(
        [
            sp.GetRequiredService<Reports.HtmlReportRenderer>(),
            sp.GetRequiredService<Reports.OpenXmlReportRenderer>(),
            sp.GetRequiredService<Reports.PdfReportRenderer>(),
            sp.GetRequiredService<Reports.CsvReportRenderer>(),
            sp.GetRequiredService<Reports.XlsxReportRenderer>(),
        ]));
        // M13 audit committee: AC-pack assembler + generation service + format-dispatching renderer (HTML always +
        // DOCX via OpenXml), mirroring the M8 wiring (concrete renderers under their own types; composite built explicitly).
        services.AddScoped<Application.Ac.Generation.AcPackContentAssembler>();
        services.AddScoped<Application.Ac.Generation.AcPackGenerationService>();
        services.AddScoped<Ac.HtmlAcPackRenderer>();
        services.AddScoped<Ac.OpenXmlAcPackRenderer>();
        services.AddScoped<Application.Abstractions.Ac.IAcPackRenderer>(sp => new Ac.CompositeAcPackRenderer(
            [sp.GetRequiredService<Ac.HtmlAcPackRenderer>(), sp.GetRequiredService<Ac.OpenXmlAcPackRenderer>()]));
        // M12 configuration: active-config provider (memory-cached, invalidated on activate/rollback) + the snapshotter
        // used to stamp exception_defaults provenance at raise. IExceptionDefaults now reads the active config behind
        // the same (unchanged) interface, falling back to the hardcoded defaults.
        services.AddScoped<Application.Abstractions.IActiveConfigurationProvider, Configuration.CachedActiveConfigurationProvider>();
        services.AddScoped<Application.Abstractions.IConfigurationSnapshotter, Configuration.ConfigurationSnapshotter>();
        services.AddScoped<Application.Abstractions.IExceptionDefaults, Exceptions.ConfigBackedExceptionDefaults>();
        services.AddSingleton<Application.Abstractions.Storage.IFileStorage, Storage.LocalDiskFileStorage>();
        services.AddSingleton<Application.Abstractions.Storage.IFileSignatureInspector, Storage.EvidenceFileSignatureInspector>();
        services.AddScoped<Application.Abstractions.Universe.ITaxonomyProvider, Universe.TaxonomyProvider>();
        services.AddScoped<Application.Abstractions.Universe.ICoverageQueryService, Universe.CoverageQueryService>();
        services.AddScoped<Application.Abstractions.Analytics.IAnalyticsQueryService, Analytics.AnalyticsQueryService>();
        services.AddScoped<Application.Abstractions.Analytics.IAnalyticsSnapshotStore, Analytics.AnalyticsSnapshotStore>();
        services.AddScoped<Application.Analytics.Services.RecurrenceClusterService>();
        services.AddScoped<Application.Analytics.Services.AnalyticsSnapshotCaptureService>();
        services.AddScoped<IAuditTrailReader, Universe.AuditTrailReader>();
        services.AddScoped<DbSeeder>();
        // Rich, interconnected DEMO dataset seeder (gated behind Database:SeedDemoData; never in production by
        // default). Depends on AppDbContext + the same Application services the API uses (report/AC-pack generation,
        // recurrence scan, audit-creation) so the demo exercises the real generation/analytics paths.
        services.AddScoped<DemoDataSeeder>();

        // M14 integrations + M15 administration adapters.
        services.Configure<ReleaseSigningOptions>(configuration.GetSection(ReleaseSigningOptions.SectionName));
        services.AddDataProtection();
        services.AddHttpClient("webhooks");
        services.AddSingleton<ICredentialProtector, DataProtectionCredentialProtector>();
        services.AddSingleton<IWebhookSender, HttpWebhookSender>();
        services.AddSingleton<IInternalNetworkPolicy, InternalNetworkPolicy>();
        services.AddSingleton<IIntegrationTester, DefaultIntegrationTester>();
        services.AddSingleton<ISiemExporter, LoggingSiemExporter>();
        services.AddSingleton<IReleasePackageVerifier, RsaReleasePackageVerifier>();
        services.AddScoped<ISystemMetricsProvider, SystemMetricsProvider>();

        // Cross-cutting
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<Application.Abstractions.ISlugGenerator, Sharing.SlugGenerator>();
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            // Resolve the connection string lazily from options (not the value captured at registration time)
            // so late-bound configuration sources are honoured. AbortOnConnectFail=false so a transient Redis
            // blip at connect time doesn't permanently break the singleton multiplexer — it keeps retrying.
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;
            var redisConfig = ConfigurationOptions.Parse(options.ConnectionString);
            redisConfig.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisConfig);
        });
        services.AddScoped<ITokenDenylist, RedisTokenDenylist>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddSingleton<ISessionTokenService, SessionTokenService>();

        // M10 notifications: the composite dispatcher logs each event and enqueues the notification pipeline.
        // Scoped (not singleton): it consumes the scoped ICurrentUser and is only resolved by the scoped
        // AuditingSaveChangesInterceptor — a singleton here is a captive dependency that fails ValidateScopes.
        services.AddScoped<IDomainEventDispatcher, Messaging.CompositeDomainEventDispatcher>();
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddScoped<INotificationRuleRepository, NotificationRuleRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationDispatchRepository, NotificationDispatchRepository>();
        services.AddScoped<Messaging.NotificationIngestJob>();
        services.AddSingleton<Application.Abstractions.Notifications.ITemplateRenderer, Notifications.SimpleTemplateRenderer>();
        if (identityOptions.UseDevelopmentProvider)
        {
            services.AddScoped<Application.Abstractions.Notifications.IEmailSender, Notifications.LoggingEmailSender>();
            services.AddScoped<Application.Abstractions.Notifications.ISmsSender, Notifications.LoggingSmsSender>();
        }
        else
        {
            services.AddScoped<Application.Abstractions.Notifications.IEmailSender, Notifications.SmtpEmailSender>();
            services.AddScoped<Application.Abstractions.Notifications.ISmsSender, Notifications.HttpSmsSender>();
        }

        // Identity provider: seeded Development users locally, Active Directory over LDAPS, or the bank's
        // AD-over-REST gateway — selected by Identity:Provider.
        switch (identityOptions.Kind)
        {
            case IdentityProviderKind.ActiveDirectory:
                services.AddScoped<IIdentityProvider, ActiveDirectoryIdentityProvider>();
                break;

            case IdentityProviderKind.ActiveDirectoryApi:
                var adApiOptions = configuration.GetSection(ActiveDirectoryApiOptions.SectionName).Get<ActiveDirectoryApiOptions>()
                    ?? new ActiveDirectoryApiOptions();
                services.AddHttpClient<ActiveDirectoryApiIdentityProvider>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(adApiOptions.TimeoutSeconds, 1, 120));
                    if (!string.IsNullOrWhiteSpace(adApiOptions.ApiKeyHeader) && !string.IsNullOrWhiteSpace(adApiOptions.ApiKey))
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(adApiOptions.ApiKeyHeader, adApiOptions.ApiKey);
                    }
                });
                services.AddScoped<IIdentityProvider>(sp => sp.GetRequiredService<ActiveDirectoryApiIdentityProvider>());
                break;

            default:
                services.AddScoped<IIdentityProvider, DevIdentityProvider>();
                break;
        }

        return services;
    }
}
