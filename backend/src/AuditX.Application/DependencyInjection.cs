using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Application.Identity.Roles;
using AuditX.Application.Identity.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Application;

/// <summary>Registers the application layer: the dispatcher, command/query handlers, validators, and application services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        // Application services (shared orchestration used by multiple handlers).
        services.AddScoped<AuthSessionService>();
        services.AddScoped<RoleWriteService>();
        services.AddScoped<MakerCheckerGateService>();
        services.AddScoped<Integrations.Webhooks.WebhookDispatchService>();
        services.AddScoped<Audits.Services.AuditCreationService>();
        services.AddScoped<Notifications.Services.NotificationIngestService>();
        services.AddScoped<Organization.IReportingLineResolver, Organization.ReportingLineResolver>();

        // Maker-checker action replay executors.
        services.AddScoped<IPendingActionExecutor, RolePermissionChangeExecutor>();
        services.AddScoped<IPendingActionExecutor, Templates.Commands.TemplatePublishExecutor>();
        services.AddScoped<IPendingActionExecutor, Exceptions.Commands.MapApprovalExecutor>();
        services.AddScoped<IPendingActionExecutor, Sanctions.MakerChecker.SanctionsGridEditExecutor>();
        services.AddScoped<IPendingActionExecutor, Configuration.MakerChecker.ConfigActivationExecutor>();

        var assembly = typeof(DependencyInjection).Assembly;
        RegisterHandlers(services, assembly);
        RegisterValidators(services, assembly);

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var @interface in type.GetInterfaces().Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(@interface, type);
            }
        }
    }

    private static void RegisterValidators(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var @interface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            {
                services.AddScoped(@interface, type);
            }
        }
    }
}
