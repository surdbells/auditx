using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Notifications.Dtos;
using AuditX.Application.Notifications.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;
using FluentValidation;

namespace AuditX.Application.Notifications.Commands;

internal static class NotificationParsing
{
    public static NotificationChannel ParseChannel(string? value)
        => Enum.TryParse<NotificationChannel>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var c)
            ? c
            : throw new DomainException("notification.invalid_channel", $"Unknown channel '{value}'.");
}

public sealed record CreateNotificationRuleCommand(string EventType, string Name, string RecipientResolutionJson, string ChannelsJson, string TemplateKey, bool IsActive) : ICommand<NotificationRuleDto>;

public sealed class CreateNotificationRuleCommandValidator : AbstractValidator<CreateNotificationRuleCommand>
{
    public CreateNotificationRuleCommandValidator()
    {
        RuleFor(x => x.EventType).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RecipientResolutionJson).NotEmpty();
        RuleFor(x => x.TemplateKey).NotEmpty();
    }
}

public sealed class CreateNotificationRuleCommandHandler(INotificationRuleRepository rules, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateNotificationRuleCommand, NotificationRuleDto>
{
    public async Task<NotificationRuleDto> Handle(CreateNotificationRuleCommand command, CancellationToken cancellationToken)
    {
        var rule = NotificationRule.Create(command.EventType, command.Name, command.RecipientResolutionJson, command.ChannelsJson, command.TemplateKey, command.IsActive, isSystemDefault: false);
        rules.Add(rule);
        audit.Record(AuditEventTypes.NotificationRuleConfigured, AuditTargetTypes.NotificationRule, rule.Id, after: new { rule.EventType, rule.Name, rule.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rule.ToDto();
    }
}

public sealed record UpdateNotificationRuleCommand(Guid Id, string Name, string RecipientResolutionJson, string ChannelsJson, string TemplateKey, bool IsActive, string Version) : ICommand<NotificationRuleDto>;

public sealed class UpdateNotificationRuleCommandHandler(INotificationRuleRepository rules, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateNotificationRuleCommand, NotificationRuleDto>
{
    public async Task<NotificationRuleDto> Handle(UpdateNotificationRuleCommand command, CancellationToken cancellationToken)
    {
        var rule = await rules.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Notification rule", command.Id);
        if (!string.Equals(Convert.ToBase64String(rule.Version ?? []), command.Version, StringComparison.Ordinal))
        {
            throw new ConflictException("notification.concurrency_conflict", "The rule was modified by someone else; reload and retry.");
        }

        rule.Update(command.Name, command.RecipientResolutionJson, command.ChannelsJson, command.TemplateKey);
        if (command.IsActive)
        {
            rule.Activate();
        }
        else
        {
            rule.Deactivate();
        }

        audit.Record(AuditEventTypes.NotificationRuleConfigured, AuditTargetTypes.NotificationRule, rule.Id, after: new { rule.Name, rule.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rule.ToDto();
    }
}

public sealed record DeactivateNotificationRuleCommand(Guid Id) : ICommand<Unit>;

public sealed class DeactivateNotificationRuleCommandHandler(INotificationRuleRepository rules, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateNotificationRuleCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateNotificationRuleCommand command, CancellationToken cancellationToken)
    {
        var rule = await rules.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Notification rule", command.Id);
        rule.Deactivate();
        audit.Record(AuditEventTypes.NotificationRuleConfigured, AuditTargetTypes.NotificationRule, rule.Id, after: new { rule.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record CreateOrOverrideTemplateCommand(string TemplateKey, string Channel, string? SubjectTemplate, string BodyTemplate) : ICommand<NotificationTemplateDto>;

public sealed class CreateOrOverrideTemplateCommandValidator : AbstractValidator<CreateOrOverrideTemplateCommand>
{
    public CreateOrOverrideTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty();
        RuleFor(x => x.BodyTemplate).NotEmpty();
    }
}

public sealed class CreateOrOverrideTemplateCommandHandler(INotificationTemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOrOverrideTemplateCommand, NotificationTemplateDto>
{
    public async Task<NotificationTemplateDto> Handle(CreateOrOverrideTemplateCommand command, CancellationToken cancellationToken)
    {
        var channel = NotificationParsing.ParseChannel(command.Channel);
        var existing = await templates.GetByKeyChannelScopeAsync(command.TemplateKey, channel, TemplateScope.Bank, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateContent(command.SubjectTemplate, command.BodyTemplate);
            audit.Record(AuditEventTypes.NotificationTemplateOverridden, AuditTargetTypes.NotificationTemplate, existing.Id, after: new { existing.TemplateKey, version = existing.Version });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return existing.ToDto();
        }

        var template = NotificationTemplate.Create(command.TemplateKey, channel, TemplateScope.Bank, command.SubjectTemplate, command.BodyTemplate);
        templates.Add(template);
        audit.Record(AuditEventTypes.NotificationTemplateOverridden, AuditTargetTypes.NotificationTemplate, template.Id, after: new { template.TemplateKey });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

public sealed record RetryDispatchCommand(Guid Id) : ICommand<NotificationDispatchDto>;

public sealed class RetryDispatchCommandHandler(
    INotificationDispatchRepository dispatches, IEmailSender emailSender, ISmsSender smsSender, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RetryDispatchCommand, NotificationDispatchDto>
{
    public async Task<NotificationDispatchDto> Handle(RetryDispatchCommand command, CancellationToken cancellationToken)
    {
        var dispatch = await dispatches.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Notification dispatch", command.Id);
        if (dispatch.Status is not (DispatchStatus.Failed or DispatchStatus.DeadLetter))
        {
            throw new ConflictException("notification.not_retryable", "Only failed or dead-lettered dispatches can be retried.");
        }

        dispatch.Requeue();

        var result = dispatch.Channel == NotificationChannel.Email
            ? await emailSender.SendAsync(dispatch.RecipientAddress, dispatch.RenderedSubject, dispatch.RenderedBody, cancellationToken)
            : await smsSender.SendAsync(dispatch.RecipientAddress, dispatch.RenderedBody, cancellationToken);

        if (result.Success)
        {
            dispatch.RecordDelivered(clock.UtcNow, result.ProviderMessageId, result.ProviderResponse);
        }
        else
        {
            dispatch.RecordFailure(clock.UtcNow, result.Error ?? "send failed", result.IsPermanentFailure);
        }

        audit.Record(AuditEventTypes.NotificationRetried, AuditTargetTypes.NotificationDispatch, dispatch.Id, after: new { status = dispatch.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return dispatch.ToDto();
    }
}
