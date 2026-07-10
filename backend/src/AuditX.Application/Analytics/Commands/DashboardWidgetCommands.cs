using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Analytics.Dtos;
using AuditX.Application.Analytics.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Analytics;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Analytics.Commands;

internal static class DashboardConcurrency
{
    /// <summary>Optimistic-concurrency guard: the caller echoes the dashboard rowversion it last read (409 on mismatch).</summary>
    public static void EnsureVersion(this Dashboard dashboard, string expectedVersion)
    {
        if (!string.Equals(RowVersionToken.Encode(dashboard.Version), expectedVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("dashboard.concurrency_conflict", "The dashboard was modified by someone else; reload and retry.");
        }
    }

    public static WidgetType ParseWidgetType(string value)
    {
        var normalised = (value ?? string.Empty).Replace("_", string.Empty);
        return Enum.TryParse<WidgetType>(normalised, ignoreCase: true, out var type)
            ? type
            : throw new ConflictException("dashboard.invalid_widget_type", $"Unknown widget type '{value}'.");
    }
}

// ---- Add a widget (ConfigureDashboards) ----

public sealed record AddDashboardWidgetCommand(
    Guid DashboardId, string WidgetType, string MetricKey, string Title, Guid? TargetRoleId, int Position, string? ConfigJson, string Version)
    : ICommand<DashboardDetailDto>;

public sealed class AddDashboardWidgetCommandValidator : AbstractValidator<AddDashboardWidgetCommand>
{
    public AddDashboardWidgetCommandValidator()
    {
        RuleFor(x => x.WidgetType).NotEmpty();
        RuleFor(x => x.MetricKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class AddDashboardWidgetCommandHandler(IDashboardRepository dashboards, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AddDashboardWidgetCommand, DashboardDetailDto>
{
    public async Task<DashboardDetailDto> Handle(AddDashboardWidgetCommand command, CancellationToken cancellationToken)
    {
        var dashboard = await dashboards.GetByIdAsync(command.DashboardId, cancellationToken)
            ?? throw new NotFoundException("Dashboard", command.DashboardId);
        dashboard.EnsureVersion(command.Version);

        var widget = dashboard.AddWidget(
            DashboardConcurrency.ParseWidgetType(command.WidgetType),
            command.MetricKey.Trim(), command.Title.Trim(), command.TargetRoleId, command.Position, command.ConfigJson);

        audit.Record(AuditEventTypes.DashboardWidgetConfigured, AuditTargetTypes.Dashboard, dashboard.Id,
            after: new { action = "added", widgetId = widget.Id, widget.MetricKey, configurationVersion = dashboard.ConfigurationVersion });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return dashboard.ToDetailDto(dashboard.Widgets.OrderBy(w => w.Position).ThenBy(w => w.Id).Select(w => w.ToDto(null)).ToArray());
    }
}

// ---- Edit a widget (ConfigureDashboards) ----

public sealed record UpdateDashboardWidgetCommand(
    Guid DashboardId, Guid WidgetId, string WidgetType, string MetricKey, string Title, Guid? TargetRoleId, int Position, string? ConfigJson, string Version)
    : ICommand<DashboardDetailDto>;

public sealed class UpdateDashboardWidgetCommandValidator : AbstractValidator<UpdateDashboardWidgetCommand>
{
    public UpdateDashboardWidgetCommandValidator()
    {
        RuleFor(x => x.WidgetType).NotEmpty();
        RuleFor(x => x.MetricKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class UpdateDashboardWidgetCommandHandler(IDashboardRepository dashboards, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDashboardWidgetCommand, DashboardDetailDto>
{
    public async Task<DashboardDetailDto> Handle(UpdateDashboardWidgetCommand command, CancellationToken cancellationToken)
    {
        var dashboard = await dashboards.GetByIdAsync(command.DashboardId, cancellationToken)
            ?? throw new NotFoundException("Dashboard", command.DashboardId);
        dashboard.EnsureVersion(command.Version);

        dashboard.EditWidget(
            command.WidgetId,
            DashboardConcurrency.ParseWidgetType(command.WidgetType),
            command.MetricKey.Trim(), command.Title.Trim(), command.TargetRoleId, command.Position, command.ConfigJson);

        audit.Record(AuditEventTypes.DashboardWidgetConfigured, AuditTargetTypes.Dashboard, dashboard.Id,
            after: new { action = "updated", widgetId = command.WidgetId, configurationVersion = dashboard.ConfigurationVersion });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return dashboard.ToDetailDto(dashboard.Widgets.OrderBy(w => w.Position).ThenBy(w => w.Id).Select(w => w.ToDto(null)).ToArray());
    }
}

// ---- Remove a widget (ConfigureDashboards) ----

public sealed record RemoveDashboardWidgetCommand(Guid DashboardId, Guid WidgetId, string Version) : ICommand<DashboardDetailDto>;

public sealed class RemoveDashboardWidgetCommandValidator : AbstractValidator<RemoveDashboardWidgetCommand>
{
    public RemoveDashboardWidgetCommandValidator() => RuleFor(x => x.Version).NotEmpty();
}

public sealed class RemoveDashboardWidgetCommandHandler(IDashboardRepository dashboards, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveDashboardWidgetCommand, DashboardDetailDto>
{
    public async Task<DashboardDetailDto> Handle(RemoveDashboardWidgetCommand command, CancellationToken cancellationToken)
    {
        var dashboard = await dashboards.GetByIdAsync(command.DashboardId, cancellationToken)
            ?? throw new NotFoundException("Dashboard", command.DashboardId);
        dashboard.EnsureVersion(command.Version);

        dashboard.RemoveWidget(command.WidgetId);

        audit.Record(AuditEventTypes.DashboardWidgetConfigured, AuditTargetTypes.Dashboard, dashboard.Id,
            after: new { action = "removed", widgetId = command.WidgetId, configurationVersion = dashboard.ConfigurationVersion });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return dashboard.ToDetailDto(dashboard.Widgets.OrderBy(w => w.Position).ThenBy(w => w.Id).Select(w => w.ToDto(null)).ToArray());
    }
}
