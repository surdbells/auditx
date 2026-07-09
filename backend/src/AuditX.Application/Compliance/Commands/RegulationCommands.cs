using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Compliance.Dtos;
using AuditX.Application.Compliance.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Compliance;
using FluentValidation;

namespace AuditX.Application.Compliance.Commands;

// ---- Register ----

public sealed record RegisterRegulationCommand(
    string Code, string Name, string? Authority, string? Description, string? Category) : ICommand<RegulationDto>;

public sealed class RegisterRegulationCommandValidator : AbstractValidator<RegisterRegulationCommand>
{
    public RegisterRegulationCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Authority).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}

public sealed class RegisterRegulationCommandHandler(IRegulationRepository regulations, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterRegulationCommand, RegulationDto>
{
    public async Task<RegulationDto> Handle(RegisterRegulationCommand command, CancellationToken cancellationToken)
    {
        var code = command.Code.Trim();
        if (await regulations.CodeExistsAsync(code, null, cancellationToken))
        {
            throw new ConflictException("regulation.code_taken", "That regulation code is already in use.");
        }

        var regulation = Regulation.Register(code, command.Name, command.Authority, command.Description, command.Category);
        regulations.Add(regulation);
        audit.Record(AuditEventTypes.RegulationRegistered, AuditTargetTypes.Regulation, regulation.Id,
            after: new { regulation.Code, regulation.Name, regulation.Authority });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return regulation.ToDto();
    }
}

// ---- Update ----

public sealed record UpdateRegulationCommand(
    Guid Id, string Name, string? Authority, string? Description, string? Category, string Version) : ICommand<RegulationDto>;

public sealed class UpdateRegulationCommandValidator : AbstractValidator<UpdateRegulationCommand>
{
    public UpdateRegulationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Authority).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class UpdateRegulationCommandHandler(IRegulationRepository regulations, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRegulationCommand, RegulationDto>
{
    public async Task<RegulationDto> Handle(UpdateRegulationCommand command, CancellationToken cancellationToken)
    {
        var regulation = await regulations.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Regulation", command.Id);
        regulation.EnsureVersion(command.Version);
        regulation.Update(command.Name, command.Authority, command.Description, command.Category);
        audit.Record(AuditEventTypes.RegulationUpdated, AuditTargetTypes.Regulation, regulation.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return regulation.ToDto();
    }
}

// ---- Set active/retired ----

public sealed record SetRegulationStatusCommand(Guid Id, bool IsActive, string Version) : ICommand<RegulationDto>;

public sealed class SetRegulationStatusCommandHandler(IRegulationRepository regulations, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetRegulationStatusCommand, RegulationDto>
{
    public async Task<RegulationDto> Handle(SetRegulationStatusCommand command, CancellationToken cancellationToken)
    {
        var regulation = await regulations.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Regulation", command.Id);
        regulation.EnsureVersion(command.Version);
        regulation.SetActive(command.IsActive);
        audit.Record(AuditEventTypes.RegulationStatusChanged, AuditTargetTypes.Regulation, regulation.Id, after: new { command.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return regulation.ToDto();
    }
}

// ---- Delete (soft) ----

public sealed record DeleteRegulationCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteRegulationCommandHandler(
    IRegulationRepository regulations, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteRegulationCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRegulationCommand command, CancellationToken cancellationToken)
    {
        var regulation = await regulations.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Regulation", command.Id);
        regulation.EnsureVersion(command.Version);
        regulation.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.RegulationDeleted, AuditTargetTypes.Regulation, regulation.Id, before: new { regulation.Code });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
