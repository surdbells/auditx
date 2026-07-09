using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Risks.Dtos;
using AuditX.Application.Risks.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Risks;
using FluentValidation;

namespace AuditX.Application.Risks.Commands;

// ---- Register ----

public sealed record RegisterRiskCommand(
    string Title, string? Description, string Category, Guid OwnerUserId, Guid? AuditableEntityId,
    int InherentLikelihood, int InherentImpact, DateOnly? TargetDate) : ICommand<RiskDto>;

public sealed class RegisterRiskCommandValidator : AbstractValidator<RegisterRiskCommand>
{
    public RegisterRiskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.InherentLikelihood).InclusiveBetween(1, 5);
        RuleFor(x => x.InherentImpact).InclusiveBetween(1, 5);
    }
}

public sealed class RegisterRiskCommandHandler(
    IRiskRepository risks, IUserRepository users, IAuditUniverseRepository entities,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterRiskCommand, RiskDto>
{
    public async Task<RiskDto> Handle(RegisterRiskCommand command, CancellationToken cancellationToken)
    {
        await RiskGuards.EnsureOwnerAsync(users, command.OwnerUserId, cancellationToken);
        await RiskGuards.EnsureEntityAsync(entities, command.AuditableEntityId, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var risk = Risk.Register(
            command.Title, command.Description, command.Category, command.OwnerUserId, command.AuditableEntityId,
            command.InherentLikelihood, command.InherentImpact, command.TargetDate, userId, clock.UtcNow);
        risks.Add(risk);

        audit.Record(AuditEventTypes.RiskRegistered, AuditTargetTypes.Risk, risk.Id,
            after: new { risk.Title, risk.Category, risk.OwnerUserId, risk.InherentScore });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return risk.ToDto();
    }
}

// ---- Update (metadata + inherent + optional residual/treatment) ----

public sealed record UpdateRiskCommand(
    Guid Id, string Title, string? Description, string Category, Guid OwnerUserId, Guid? AuditableEntityId,
    int InherentLikelihood, int InherentImpact, int? ResidualLikelihood, int? ResidualImpact,
    string? TreatmentStrategy, string? TreatmentPlan, DateOnly? TargetDate, DateOnly? NextReviewDate, string Version)
    : ICommand<RiskDto>;

public sealed class UpdateRiskCommandValidator : AbstractValidator<UpdateRiskCommand>
{
    public UpdateRiskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.InherentLikelihood).InclusiveBetween(1, 5);
        RuleFor(x => x.InherentImpact).InclusiveBetween(1, 5);
        RuleFor(x => x.ResidualLikelihood).InclusiveBetween(1, 5).When(x => x.ResidualLikelihood.HasValue);
        RuleFor(x => x.ResidualImpact).InclusiveBetween(1, 5).When(x => x.ResidualImpact.HasValue);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class UpdateRiskCommandHandler(
    IRiskRepository risks, IUserRepository users, IAuditUniverseRepository entities,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRiskCommand, RiskDto>
{
    public async Task<RiskDto> Handle(UpdateRiskCommand command, CancellationToken cancellationToken)
    {
        var risk = await risks.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Risk", command.Id);
        risk.EnsureVersion(command.Version);
        await RiskGuards.EnsureOwnerAsync(users, command.OwnerUserId, cancellationToken);
        await RiskGuards.EnsureEntityAsync(entities, command.AuditableEntityId, cancellationToken);

        risk.Update(
            command.Title, command.Description, command.Category, command.OwnerUserId, command.AuditableEntityId,
            command.InherentLikelihood, command.InherentImpact, command.ResidualLikelihood, command.ResidualImpact,
            RiskParsing.ParseStrategy(command.TreatmentStrategy), command.TreatmentPlan, command.TargetDate, command.NextReviewDate);

        audit.Record(AuditEventTypes.RiskUpdated, AuditTargetTypes.Risk, risk.Id,
            after: new { risk.InherentScore, risk.ResidualScore, status = risk.Status.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return risk.ToDto();
    }
}

// ---- Change status ----

public sealed record ChangeRiskStatusCommand(Guid Id, string Status, string? Rationale, string Version) : ICommand<RiskDto>;

public sealed class ChangeRiskStatusCommandHandler(
    IRiskRepository risks, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeRiskStatusCommand, RiskDto>
{
    public async Task<RiskDto> Handle(ChangeRiskStatusCommand command, CancellationToken cancellationToken)
    {
        var risk = await risks.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Risk", command.Id);
        risk.EnsureVersion(command.Version);

        var before = risk.Status;
        risk.ChangeStatus(RiskParsing.ParseStatus(command.Status), command.Rationale, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        audit.Record(AuditEventTypes.RiskStatusChanged, AuditTargetTypes.Risk, risk.Id,
            before: new { status = before.ToString() }, after: new { status = risk.Status.ToString(), command.Rationale });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return risk.ToDto();
    }
}

// ---- Delete (soft) ----

public sealed record DeleteRiskCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteRiskCommandHandler(
    IRiskRepository risks, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteRiskCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRiskCommand command, CancellationToken cancellationToken)
    {
        var risk = await risks.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Risk", command.Id);
        risk.EnsureVersion(command.Version);

        risk.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.RiskDeleted, AuditTargetTypes.Risk, risk.Id, before: new { risk.Title });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal static class RiskGuards
{
    public static async Task EnsureOwnerAsync(IUserRepository users, Guid ownerUserId, CancellationToken cancellationToken)
    {
        var owner = await users.GetByIdAsync(ownerUserId, cancellationToken);
        if (owner is null || owner.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("risk.owner_not_assignable", "The owner does not exist or is deactivated.");
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
