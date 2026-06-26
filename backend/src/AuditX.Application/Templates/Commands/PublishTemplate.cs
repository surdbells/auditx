using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Application.Templates.Dtos;
using AuditX.Application.Templates.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Templates.Commands;

/// <summary>The serialized intent captured when a publish is held by a maker-checker gate.</summary>
public sealed record TemplatePublishPayload(Guid TemplateId);

/// <summary>Publish a draft template (US-M2-008). Gated by maker-checker (<c>template_publish</c>) when configured.</summary>
public sealed record PublishTemplateCommand(Guid TemplateId) : ICommand<TemplateActionResult>;

public sealed class PublishTemplateCommandHandler(
    ITemplateRepository templates,
    MakerCheckerGateService gateService,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PublishTemplateCommand, TemplateActionResult>
{
    public async Task<TemplateActionResult> Handle(PublishTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);

        // Validate publishability up front so a gate never captures an un-publishable action.
        if (template.Status != TemplateStatus.Draft)
        {
            throw new InvalidStateTransitionException("template.not_draft", "Only a draft template can be published.");
        }

        if (template.Items.Count == 0)
        {
            throw new DomainException("template.empty", "A template must have at least one item before it can be published.");
        }

        var payload = AppJson.Serialize(new TemplatePublishPayload(template.Id));
        var pendingId = await gateService.TryCaptureAsync(
            MakerCheckerActionTypes.TemplatePublish, AuditTargetTypes.Template, template.Id, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new TemplateActionResult(null, id);
        }

        var version = template.Publish(clock.UtcNow, TemplateMappings.SerializeSnapshot);
        audit.Record(AuditEventTypes.TemplatePublished, AuditTargetTypes.Template, template.Id, after: new { version = version.VersionNumber });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TemplateActionResult(template.ToDto(), null);
    }
}

/// <summary>Replays an approved <c>template_publish</c> action, attributing the publish to the maker (US-M1-022).</summary>
public sealed class TemplatePublishExecutor(ITemplateRepository templates, IAuditRecorder audit, IClock clock) : IPendingActionExecutor
{
    public string ActionType => MakerCheckerActionTypes.TemplatePublish;

    public async Task ExecuteAsync(string pendingPayloadJson, Guid makerUserId, CancellationToken cancellationToken)
    {
        var payload = AppJson.Deserialize<TemplatePublishPayload>(pendingPayloadJson);
        var template = await templates.GetByIdAsync(payload.TemplateId, cancellationToken)
            ?? throw new NotFoundException("Template", payload.TemplateId);

        var version = template.Publish(clock.UtcNow, TemplateMappings.SerializeSnapshot);
        audit.RecordAs(ActorType.User, actorSystemLabel: null, actorUserId: makerUserId,
            AuditEventTypes.TemplatePublished, AuditTargetTypes.Template, template.Id, after: new { version = version.VersionNumber });
    }
}
