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
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        services.AddScoped<IAuditUniverseRepository, AuditUniverseRepository>();
        services.AddScoped<IRiskDimensionRepository, RiskDimensionRepository>();
        services.AddScoped<IEntityTypeTaxonomyRepository, EntityTypeTaxonomyRepository>();
        services.AddScoped<IAnnualPlanRepository, AnnualPlanRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IEvidenceRepository, EvidenceRepository>();
        services.AddScoped<IExceptionRepository, ExceptionRepository>();
        services.AddScoped<Application.Abstractions.IExceptionDefaults, Exceptions.ConfigBackedExceptionDefaults>();
        services.AddSingleton<Application.Abstractions.Storage.IFileStorage, Storage.LocalDiskFileStorage>();
        services.AddSingleton<Application.Abstractions.Storage.IFileSignatureInspector, Storage.EvidenceFileSignatureInspector>();
        services.AddScoped<Application.Abstractions.Universe.ITaxonomyProvider, Universe.TaxonomyProvider>();
        services.AddScoped<Application.Abstractions.Universe.ICoverageQueryService, Universe.CoverageQueryService>();
        services.AddScoped<IAuditTrailReader, Universe.AuditTrailReader>();
        services.AddScoped<DbSeeder>();

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

        // Identity provider: Active Directory in production, the seeded Development provider locally.
        if (identityOptions.UseDevelopmentProvider)
        {
            services.AddScoped<IIdentityProvider, DevIdentityProvider>();
        }
        else
        {
            services.AddScoped<IIdentityProvider, ActiveDirectoryIdentityProvider>();
        }

        return services;
    }
}
