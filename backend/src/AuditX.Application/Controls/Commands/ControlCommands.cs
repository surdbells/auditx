using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Controls.Dtos;
using AuditX.Application.Controls.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Controls;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Controls.Commands;

// ---- Register ----

public sealed record RegisterControlCommand(
    string Code, string Title, string? Description, string ControlType, string Frequency,
    Guid OwnerUserId, Guid? AuditableEntityId) : ICommand<ControlDto>;

public sealed class RegisterControlCommandValidator : AbstractValidator<RegisterControlCommand>
{
    public RegisterControlCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ControlType).NotEmpty();
        RuleFor(x => x.Frequency).NotEmpty();
        RuleFor(x => x.OwnerUserId).NotEmpty();
    }
}

public sealed class RegisterControlCommandHandler(
    IControlRepository controls, IUserRepository users, IAuditUniverseRepository entities,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterControlCommand, ControlDto>
{
    public async Task<ControlDto> Handle(RegisterControlCommand command, CancellationToken cancellationToken)
    {
        var code = command.Code.Trim();
        if (await controls.CodeExistsAsync(code, null, cancellationToken))
        {
            throw new ConflictException("control.code_taken", "That control code is already in use.");
        }

        await ControlGuards.EnsureOwnerAsync(users, command.OwnerUserId, cancellationToken);
        await ControlGuards.EnsureEntityAsync(entities, command.AuditableEntityId, cancellationToken);

        var control = Control.Register(code, command.Title, command.Description,
            ControlParsing.ParseType(command.ControlType), ControlParsing.ParseFrequency(command.Frequency),
            command.OwnerUserId, command.AuditableEntityId);
        controls.Add(control);

        audit.Record(AuditEventTypes.ControlRegistered, AuditTargetTypes.Control, control.Id,
            after: new { control.Code, control.Title, controlType = control.ControlType.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return control.ToDto();
    }
}

// ---- Update ----

public sealed record UpdateControlCommand(
    Guid Id, string Title, string? Description, string ControlType, string Frequency, Guid OwnerUserId,
    Guid? AuditableEntityId, string? Effectiveness, DateOnly? LastTestedDate, string Version) : ICommand<ControlDto>;

public sealed class UpdateControlCommandValidator : AbstractValidator<UpdateControlCommand>
{
    public UpdateControlCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ControlType).NotEmpty();
        RuleFor(x => x.Frequency).NotEmpty();
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class UpdateControlCommandHandler(
    IControlRepository controls, IUserRepository users, IAuditUniverseRepository entities,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateControlCommand, ControlDto>
{
    public async Task<ControlDto> Handle(UpdateControlCommand command, CancellationToken cancellationToken)
    {
        var control = await controls.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Control", command.Id);
        control.EnsureVersion(command.Version);
        await ControlGuards.EnsureOwnerAsync(users, command.OwnerUserId, cancellationToken);
        await ControlGuards.EnsureEntityAsync(entities, command.AuditableEntityId, cancellationToken);

        control.Update(command.Title, command.Description,
            ControlParsing.ParseType(command.ControlType), ControlParsing.ParseFrequency(command.Frequency),
            command.OwnerUserId, command.AuditableEntityId,
            ControlParsing.ParseEffectiveness(command.Effectiveness), command.LastTestedDate);

        audit.Record(AuditEventTypes.ControlUpdated, AuditTargetTypes.Control, control.Id,
            after: new { effectiveness = control.Effectiveness.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return control.ToDto();
    }
}

// ---- Set active/retired ----

public sealed record SetControlStatusCommand(Guid Id, bool IsActive, string Version) : ICommand<ControlDto>;

public sealed class SetControlStatusCommandHandler(IControlRepository controls, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetControlStatusCommand, ControlDto>
{
    public async Task<ControlDto> Handle(SetControlStatusCommand command, CancellationToken cancellationToken)
    {
        var control = await controls.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Control", command.Id);
        control.EnsureVersion(command.Version);
        control.SetActive(command.IsActive);
        audit.Record(AuditEventTypes.ControlStatusChanged, AuditTargetTypes.Control, control.Id, after: new { command.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return control.ToDto();
    }
}

// ---- Delete (soft) ----

public sealed record DeleteControlCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteControlCommandHandler(
    IControlRepository controls, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteControlCommand, Unit>
{
    public async Task<Unit> Handle(DeleteControlCommand command, CancellationToken cancellationToken)
    {
        var control = await controls.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Control", command.Id);
        control.EnsureVersion(command.Version);
        control.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.ControlDeleted, AuditTargetTypes.Control, control.Id, before: new { control.Code });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal static class ControlGuards
{
    public static async Task EnsureOwnerAsync(IUserRepository users, Guid ownerUserId, CancellationToken cancellationToken)
    {
        var owner = await users.GetByIdAsync(ownerUserId, cancellationToken);
        if (owner is null || owner.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("control.owner_not_assignable", "The owner does not exist or is deactivated.");
        }
    }

    public static async Task EnsureEntityAsync(IAuditUniverseRepository entities, Guid? auditableEntityId, CancellationToken cancellationToken)
    {
        if (auditableEntityId is { } id && await entities.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Entity", id);
        }
    }
}
