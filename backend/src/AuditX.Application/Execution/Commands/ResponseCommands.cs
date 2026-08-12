using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Application.Execution.Services;
using AuditX.Domain.Audits;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using FluentValidation;

namespace AuditX.Application.Execution.Commands;

public sealed record SubmitResponseCommand(Guid AuditId, Guid ItemId, string? Verdict, string? Comment, string? ValueJson, bool IsDraft, string Version, string? Observation = null, string? Recommendation = null) : ICommand<ChecklistResponseDto>;

public sealed class SubmitResponseCommandValidator : AbstractValidator<SubmitResponseCommand>
{
    public SubmitResponseCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        // Finalise rules are type-aware and enforced in the domain (verdict types need a verdict; value types
        // need a value). Both value + verdict are optional at the transport layer.
    }
}

public sealed class SubmitResponseCommandHandler(
    IAuditRepository audits,
    IPermissionResolver permissions,
    IBankSettingsRepository bankSettings,
    IResponseScoringService scoring,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitResponseCommand, ChecklistResponseDto>
{
    public async Task<ChecklistResponseDto> Handle(SubmitResponseCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);

        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var item = entity.ChecklistItems.FirstOrDefault(i => i.Id == command.ItemId) ?? throw new NotFoundException("Checklist item", command.ItemId);
        var isOverride = await RespondAuthorization.EnsureCanRespondAsync(entity, item, userId, permissions, cancellationToken);

        var verdict = RespondAuthorization.ParseVerdict(command.Verdict, command.IsDraft);
        var settings = await bankSettings.GetAsync(cancellationToken);

        var mutation = entity.RecordResponse(command.ItemId, verdict, command.Comment, command.IsDraft, userId, settings.RequireCommentOnPass, clock.UtcNow, command.ValueJson, command.Observation, command.Recommendation);

        // Post-response scoring: computed here (the aggregate can't resolve a RatingScale) and applied to the
        // just-recorded response before it's persisted/snapshotted.
        var score = await scoring.ComputeScoreAsync(
            item.ResponseType, item.ResponseConfigJson, mutation.Response.Verdict, mutation.Response.ValueJson, command.IsDraft, cancellationToken);
        entity.SetResponseScore(command.ItemId, score);

        audit.Record(
            isOverride ? AuditEventTypes.ItemResponseOverridden : AuditEventTypes.ItemResponded,
            AuditTargetTypes.ChecklistResponse, mutation.Response.Id,
            before: Snapshot(mutation.Before), after: Snapshot(mutation.Response.ToState()));

        // Finalising the last item auto-transitions to Under Review inside the aggregate; record that
        // lifecycle change in the trail too, so the auto path matches the explicit transition path.
        if (mutation.AutoTransitioned)
        {
            audit.Record(AuditEventTypes.AuditTransitioned, AuditTargetTypes.Audit, entity.Id, after: new { status = entity.Status.ToSnake() });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return mutation.Response.ToDto();
    }

    private static object? Snapshot(ResponseState? state)
        => state is null ? null : new { verdict = state.Verdict is { } v ? v.ToSnake() : null, state.Comment, state.Observation, state.Recommendation, state.ValueJson, state.IsDraft, state.Version, state.Score };
}

public sealed record DiscardDraftCommand(Guid AuditId, Guid ItemId, string Version) : ICommand<Unit>;

public sealed class DiscardDraftCommandHandler(
    IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<DiscardDraftCommand, Unit>
{
    public async Task<Unit> Handle(DiscardDraftCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var response = entity.Responses.FirstOrDefault(r => r.ChecklistItemId == command.ItemId)
            ?? throw new NotFoundException("Response", command.ItemId);

        var isManager = await permissions.HasPermissionAsync(userId, PermissionKeys.ManageAudit, entity.Id.ToString(), cancellationToken);
        if (response.ResponderUserId != userId && !isManager)
        {
            throw new ForbiddenAccessException("Only the draft's author may discard it.");
        }

        var responseId = entity.DiscardDraft(command.ItemId);
        audit.Record(AuditEventTypes.DraftDiscarded, AuditTargetTypes.ChecklistResponse, responseId, payload: new { command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
