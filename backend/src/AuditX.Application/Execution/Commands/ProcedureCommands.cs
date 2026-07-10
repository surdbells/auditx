using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Execution.Dtos;
using AuditX.Application.Execution.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Execution;
using FluentValidation;

namespace AuditX.Application.Execution.Commands;

// ---- Record a procedure ----

public sealed record RecordProcedureCommand(
    Guid AuditId, string Type, Guid? ChecklistItemId, DateOnly PerformedOn, string Summary, string? Counterparty,
    int? Population, int? SampleSize, int? ItemsTested, int? ExceptionsFound, string? Method) : ICommand<AuditProcedureDto>;

public sealed class RecordProcedureCommandValidator : AbstractValidator<RecordProcedureCommand>
{
    public RecordProcedureCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Counterparty).MaximumLength(300);
    }
}

public sealed class RecordProcedureCommandHandler(
    IAuditRepository audits,
    IAuditProcedureRepository procedures,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RecordProcedureCommand, AuditProcedureDto>
{
    public async Task<AuditProcedureDto> Handle(RecordProcedureCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", command.AuditId);
        await ProcedureAccess.EnsureCanRecordAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        if (command.ChecklistItemId is { } itemId && auditEntity.ChecklistItems.All(i => i.Id != itemId))
        {
            throw new NotFoundException("Checklist item", itemId);
        }

        var type = ProcedureParsing.ParseType(command.Type);
        var method = ProcedureParsing.ParseMethod(command.Method);
        var procedure = AuditProcedure.Record(
            command.AuditId, type, command.ChecklistItemId, userId, command.PerformedOn, command.Summary, command.Counterparty,
            command.Population, command.SampleSize, command.ItemsTested, command.ExceptionsFound, method);
        procedures.Add(procedure);

        audit.Record(AuditEventTypes.ProcedureRecorded, AuditTargetTypes.AuditProcedure, procedure.Id,
            after: new { procedure.AuditId, type = type.ToString(), procedure.PerformedByUserId });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return procedure.ToDto();
    }
}

// ---- Delete a procedure (soft) ----

public sealed record DeleteProcedureCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteProcedureCommandHandler(
    IAuditProcedureRepository procedures, IAuditRepository audits, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteProcedureCommand, Unit>
{
    public async Task<Unit> Handle(DeleteProcedureCommand command, CancellationToken cancellationToken)
    {
        var procedure = await procedures.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Procedure", command.Id);
        var auditEntity = await audits.GetByIdAsync(procedure.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", procedure.AuditId);
        await ProcedureAccess.EnsureCanModifyAsync(procedure, auditEntity, currentUser.UserId, permissions, cancellationToken);
        procedure.EnsureVersion(command.Version);

        procedure.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.ProcedureDeleted, AuditTargetTypes.AuditProcedure, procedure.Id, before: new { procedure.AuditId, type = procedure.Type.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
