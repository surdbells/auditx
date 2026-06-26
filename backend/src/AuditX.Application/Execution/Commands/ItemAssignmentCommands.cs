using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using FluentValidation;

namespace AuditX.Application.Execution.Commands;

public sealed record AssignItemCommand(Guid AuditId, Guid ItemId, Guid? AssigneeUserId, string Version) : ICommand<AuditDto>;

public sealed class AssignItemCommandHandler(IAuditRepository audits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AssignItemCommand, AuditDto>
{
    public async Task<AuditDto> Handle(AssignItemCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        entity.AssignItem(command.ItemId, command.AssigneeUserId);
        audit.Record(AuditEventTypes.ItemAssigned, AuditTargetTypes.AuditChecklistItem, command.ItemId, payload: new { command.AssigneeUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record AuditItemAssignment(Guid ItemId, Guid? AssigneeUserId);

public sealed record BulkReassignItemsCommand(Guid AuditId, IReadOnlyList<AuditItemAssignment> Assignments, string Version) : ICommand<AuditDto>;

public sealed class BulkReassignItemsCommandValidator : AbstractValidator<BulkReassignItemsCommand>
{
    public BulkReassignItemsCommandValidator()
    {
        RuleFor(x => x.Assignments).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class BulkReassignItemsCommandHandler(IAuditRepository audits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<BulkReassignItemsCommand, AuditDto>
{
    public async Task<AuditDto> Handle(BulkReassignItemsCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);

        // Atomic: any invalid assignment throws before the single SaveChanges, rolling back the whole batch.
        foreach (var assignment in command.Assignments)
        {
            entity.AssignItem(assignment.ItemId, assignment.AssigneeUserId);
        }

        audit.Record(AuditEventTypes.ItemsBulkReassigned, AuditTargetTypes.Audit, entity.Id, payload: new { count = command.Assignments.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record RecordFailJudgementCommand(Guid AuditId, Guid ItemId, string Justification, string Version) : ICommand<AuditDto>;

public sealed class RecordFailJudgementCommandValidator : AbstractValidator<RecordFailJudgementCommand>
{
    public RecordFailJudgementCommandValidator()
    {
        RuleFor(x => x.Justification).NotEmpty().MinimumLength(10);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class RecordFailJudgementCommandHandler(
    IAuditRepository audits, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordFailJudgementCommand, AuditDto>
{
    public async Task<AuditDto> Handle(RecordFailJudgementCommand command, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(command.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", command.AuditId);
        entity.EnsureVersion(command.Version);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        entity.RecordFailJudgement(command.ItemId, command.Justification, userId, clock.UtcNow);
        audit.Record(AuditEventTypes.FailJudgementRecorded, AuditTargetTypes.AuditChecklistItem, command.ItemId, payload: new { command.Justification });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}
