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
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
        services.AddScoped<ITokenDenylist, RedisTokenDenylist>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        services.AddSingleton<IDomainEventDispatcher, InProcessDomainEventDispatcher>();

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
