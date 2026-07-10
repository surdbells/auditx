using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Integrations;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Integrations.Dtos;
using AuditX.Application.Integrations.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;
using FluentValidation;

namespace AuditX.Application.Integrations.Webhooks;

public sealed record CreateWebhookSubscriptionCommand(
    string DestinationUrl, IReadOnlyList<string> SubscribedEventTypes, string HmacSecret, string? RetryPolicyJson)
    : ICommand<WebhookSubscriptionDto>;

public sealed class CreateWebhookSubscriptionCommandValidator : AbstractValidator<CreateWebhookSubscriptionCommand>
{
    public CreateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.DestinationUrl).NotEmpty();
        RuleFor(x => x.HmacSecret).NotEmpty().MinimumLength(16);
        RuleFor(x => x.SubscribedEventTypes).NotEmpty();
    }
}

public sealed class CreateWebhookSubscriptionCommandHandler(
    IWebhookRepository webhooks,
    IInternalNetworkPolicy networkPolicy,
    ICredentialProtector protector,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWebhookSubscriptionCommand, WebhookSubscriptionDto>
{
    public async Task<WebhookSubscriptionDto> Handle(CreateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        // Egress containment: webhook destinations must be on the bank's internal network (US-M14-015).
        if (!networkPolicy.IsInternal(command.DestinationUrl))
        {
            throw new ConflictException("webhook_external_destination", "Webhook destinations must be on the bank's internal network.");
        }

        var secret = protector.Protect(command.HmacSecret);
        var subscription = WebhookSubscription.Create(command.DestinationUrl.Trim(), command.SubscribedEventTypes, secret, command.RetryPolicyJson);
        webhooks.AddSubscription(subscription);
        audit.Record(AuditEventTypes.WebhookSubscribed, AuditTargetTypes.WebhookSubscription, subscription.Id,
            after: new { subscription.DestinationUrl, events = subscription.SubscribedEventTypes });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return subscription.ToDto();
    }
}

public sealed record DeleteWebhookSubscriptionCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteWebhookSubscriptionCommandHandler(
    IWebhookRepository webhooks, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteWebhookSubscriptionCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var subscription = await webhooks.GetSubscriptionAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Webhook subscription", command.Id);
        subscription.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.WebhookUnsubscribed, AuditTargetTypes.WebhookSubscription, subscription.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RetryWebhookDeliveryCommand(Guid Id) : ICommand<Unit>;

public sealed class RetryWebhookDeliveryCommandHandler(
    IWebhookRepository webhooks, WebhookDispatchService dispatcher, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RetryWebhookDeliveryCommand, Unit>
{
    public async Task<Unit> Handle(RetryWebhookDeliveryCommand command, CancellationToken cancellationToken)
    {
        var delivery = await webhooks.GetDeliveryAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Webhook delivery", command.Id);
        if (delivery.Status != WebhookDeliveryStatus.DeadLetter)
        {
            throw new ConflictException("delivery_not_dead_letter", "Only dead-lettered deliveries can be manually retried.");
        }

        var subscription = await webhooks.GetSubscriptionAsync(delivery.SubscriptionId, cancellationToken)
            ?? throw new NotFoundException("Webhook subscription", delivery.SubscriptionId);

        delivery.Requeue();
        await dispatcher.AttemptAsync(delivery, subscription, cancellationToken);
        audit.Record(AuditEventTypes.WebhookRetried, AuditTargetTypes.WebhookDelivery, delivery.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ListWebhookSubscriptionsQuery : IQuery<IReadOnlyList<WebhookSubscriptionDto>>;

public sealed class ListWebhookSubscriptionsQueryHandler(IWebhookRepository webhooks)
    : IQueryHandler<ListWebhookSubscriptionsQuery, IReadOnlyList<WebhookSubscriptionDto>>
{
    public async Task<IReadOnlyList<WebhookSubscriptionDto>> Handle(ListWebhookSubscriptionsQuery query, CancellationToken cancellationToken)
        => (await webhooks.GetAllSubscriptionsAsync(cancellationToken)).Select(s => s.ToDto()).ToArray();
}

public sealed record ListWebhookDeliveriesQuery(string? Status, Guid? SubscriptionId, int? Page, int? PageSize)
    : IQuery<PagedResult<WebhookDeliveryDto>>;

public sealed class ListWebhookDeliveriesQueryHandler(IWebhookRepository webhooks)
    : IQueryHandler<ListWebhookDeliveriesQuery, PagedResult<WebhookDeliveryDto>>
{
    public async Task<PagedResult<WebhookDeliveryDto>> Handle(ListWebhookDeliveriesQuery query, CancellationToken cancellationToken)
    {
        WebhookDeliveryStatus? status = query.Status is null
            ? null
            : Enum.TryParse<WebhookDeliveryStatus>(query.Status.Replace("_", string.Empty), ignoreCase: true, out var parsed)
                ? parsed
                : throw new ConflictException("invalid_delivery_status", $"Unknown delivery status '{query.Status}'.");

        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await webhooks.GetDeliveriesAsync(status, query.SubscriptionId, page, cancellationToken);
        return result.Map(d => d.ToDto());
    }
}
