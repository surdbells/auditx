using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Ac;
using AuditX.Domain.AuditTrail;
using FluentValidation;

namespace AuditX.Application.Ac.Commands;

// ---- Create an AC action item (ACMember) ----

public sealed record CreateAcActionItemCommand(
    string Title, string? Description, Guid? AssignedToUserId, DateOnly? DueDate) : ICommand<AcActionItemDto>;

public sealed class CreateAcActionItemCommandValidator : AbstractValidator<CreateAcActionItemCommand>
{
    public CreateAcActionItemCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(10000);
    }
}

public sealed class CreateAcActionItemCommandHandler(
    IAcActionItemRepository items, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateAcActionItemCommand, AcActionItemDto>
{
    public async Task<AcActionItemDto> Handle(CreateAcActionItemCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var item = AcActionItem.Create(command.Title, command.Description, command.AssignedToUserId, command.DueDate, actorId);
        items.Add(item);

        audit.Record(AuditEventTypes.AcActionItemCreated, AuditTargetTypes.AcActionItem, item.Id, after: new { item.Title });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return item.ToDto();
    }
}

// ---- Update an AC action item: in-progress or close-with-response (CIA) ----

/// <summary>
/// Update an action item (M13). <c>MarkInProgress</c> moves open → in_progress; supplying a
/// <see cref="ClosureResponse"/> closes it (blank → 422 via the domain guard). CIA-gated.
/// </summary>
public sealed record UpdateAcActionItemCommand(
    Guid AcActionItemId, bool MarkInProgress, string? ClosureResponse) : ICommand<AcActionItemDto>;

public sealed class UpdateAcActionItemCommandValidator : AbstractValidator<UpdateAcActionItemCommand>
{
    public UpdateAcActionItemCommandValidator()
    {
        RuleFor(x => x.AcActionItemId).NotEmpty();
        RuleFor(x => x).Must(x => x.MarkInProgress || !string.IsNullOrWhiteSpace(x.ClosureResponse))
            .WithMessage("Provide a closure response to close the item, or set mark_in_progress.")
            .WithErrorCode("ac_action_item.no_op");
        RuleFor(x => x.ClosureResponse).MaximumLength(10000);
    }
}

public sealed class UpdateAcActionItemCommandHandler(
    IAcActionItemRepository items, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateAcActionItemCommand, AcActionItemDto>
{
    public async Task<AcActionItemDto> Handle(UpdateAcActionItemCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var item = await items.GetByIdAsync(command.AcActionItemId, cancellationToken) ?? throw new NotFoundException("AC action item", command.AcActionItemId);

        // Closure (with a required response) takes precedence; otherwise mark in progress.
        if (!string.IsNullOrWhiteSpace(command.ClosureResponse))
        {
            item.Close(command.ClosureResponse, actorId, clock.UtcNow);
        }
        else if (command.MarkInProgress)
        {
            item.MarkInProgress();
        }

        audit.Record(AuditEventTypes.AcActionItemUpdated, AuditTargetTypes.AcActionItem, item.Id,
            after: new { status = item.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return item.ToDto();
    }
}

// ---- Acknowledge a closed action item (ACChair; terminal) ----

public sealed record AcknowledgeAcActionItemClosureCommand(Guid AcActionItemId) : ICommand<AcActionItemDto>;

public sealed class AcknowledgeAcActionItemClosureCommandValidator : AbstractValidator<AcknowledgeAcActionItemClosureCommand>
{
    public AcknowledgeAcActionItemClosureCommandValidator() => RuleFor(x => x.AcActionItemId).NotEmpty();
}

public sealed class AcknowledgeAcActionItemClosureCommandHandler(
    IAcActionItemRepository items, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AcknowledgeAcActionItemClosureCommand, AcActionItemDto>
{
    public async Task<AcActionItemDto> Handle(AcknowledgeAcActionItemClosureCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var item = await items.GetByIdAsync(command.AcActionItemId, cancellationToken) ?? throw new NotFoundException("AC action item", command.AcActionItemId);

        item.AcknowledgeClosure(actorId, clock.UtcNow);
        audit.Record(AuditEventTypes.AcActionItemClosureAcknowledged, AuditTargetTypes.AcActionItem, item.Id, after: new { status = item.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return item.ToDto();
    }
}
