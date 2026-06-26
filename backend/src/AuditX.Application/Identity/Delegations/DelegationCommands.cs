using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Identity.Delegations;

/// <summary>Create a time-bounded delegation: <paramref name="FromUserId"/> grants <paramref name="ToUserId"/> a role for a window (US-M1-026).</summary>
public sealed record CreateDelegationCommand(
    Guid FromUserId,
    Guid ToUserId,
    Guid RoleId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate) : ICommand<DelegationDto>;

public sealed class CreateDelegationCommandValidator : AbstractValidator<CreateDelegationCommand>
{
    public CreateDelegationCommandValidator()
    {
        RuleFor(x => x.FromUserId).NotEmpty();
        RuleFor(x => x.ToUserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("Delegation end must be after its start.");
        RuleFor(x => x.ToUserId).NotEqual(x => x.FromUserId)
            .WithMessage("A user cannot delegate a role to themselves.");
    }
}

public sealed class CreateDelegationCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDelegationCommand, DelegationDto>
{
    public async Task<DelegationDto> Handle(CreateDelegationCommand command, CancellationToken cancellationToken)
    {
        _ = await users.GetByIdAsync(command.FromUserId, cancellationToken) ?? throw new NotFoundException("User", command.FromUserId);
        var toUser = await users.GetByIdAsync(command.ToUserId, cancellationToken) ?? throw new NotFoundException("User", command.ToUserId);
        var role = await roles.GetByIdAsync(command.RoleId, cancellationToken) ?? throw new NotFoundException("Role", command.RoleId);

        var delegation = UserRole.Delegate(toUser.Id, role.Id, command.FromUserId, command.StartDate, command.EndDate);
        userRoles.Add(delegation);
        toUser.MarkActiveOnFirstRole();

        audit.Record(AuditEventTypes.DelegationStarted, AuditTargetTypes.UserRole, delegation.Id,
            payload: new { fromUserId = command.FromUserId, toUserId = toUser.Id, roleId = role.Id, role.Name, command.StartDate, command.EndDate });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAsync(toUser.Id, cancellationToken);

        return new DelegationDto(delegation.Id, toUser.Id, role.Id, role.Name, command.FromUserId,
            delegation.DelegationStart, delegation.DelegationEnd, delegation.IsActive);
    }
}

/// <summary>Terminate a delegation early (US-M1-029).</summary>
public sealed record RevokeDelegationCommand(Guid DelegationId) : ICommand<Unit>;

public sealed class RevokeDelegationCommandHandler(
    IUserRoleRepository userRoles,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeDelegationCommand, Unit>
{
    public async Task<Unit> Handle(RevokeDelegationCommand command, CancellationToken cancellationToken)
    {
        var delegation = await userRoles.GetByIdAsync(command.DelegationId, cancellationToken)
            ?? throw new NotFoundException("Delegation", command.DelegationId);

        if (!delegation.IsDelegation)
        {
            throw new ConflictException("not_a_delegation", "The specified assignment is not a delegation.");
        }

        delegation.Deactivate();
        audit.Record(AuditEventTypes.DelegationRevoked, AuditTargetTypes.UserRole, delegation.Id,
            payload: new { toUserId = delegation.UserId, roleId = delegation.RoleId, revokedBy = currentUser.UserId });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAsync(delegation.UserId, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Expire delegations whose window has elapsed (US-M1-028). Invoked hourly by a background job.</summary>
public sealed record ExpireDelegationsCommand : ICommand<int>;

public sealed class ExpireDelegationsCommandHandler(
    IUserRoleRepository userRoles,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ExpireDelegationsCommand, int>
{
    public async Task<int> Handle(ExpireDelegationsCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expired = await userRoles.GetExpiredDelegationsAsync(now, cancellationToken);
        var affectedUserIds = new HashSet<Guid>();

        foreach (var delegation in expired)
        {
            if (delegation.ExpireIfElapsed(now))
            {
                affectedUserIds.Add(delegation.UserId);
                audit.RecordAs(ActorType.System, "delegation-expiry-job", null,
                    AuditEventTypes.DelegationEnded, AuditTargetTypes.UserRole, delegation.Id,
                    payload: new { toUserId = delegation.UserId, roleId = delegation.RoleId });
            }
        }

        if (affectedUserIds.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            foreach (var userId in affectedUserIds)
            {
                await permissions.InvalidateAsync(userId, cancellationToken);
            }
        }

        return affectedUserIds.Count;
    }
}
