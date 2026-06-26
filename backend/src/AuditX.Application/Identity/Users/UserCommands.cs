using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Identity.Users;

/// <summary>Deactivate a user, preserving their audit trail (US-M15-001).</summary>
public sealed record DeactivateUserCommand(Guid UserId) : ICommand<Unit>;

public sealed class DeactivateUserCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateUserCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        var before = new { status = user.Status.ToString() };
        user.Deactivate(currentUser.UserId);
        audit.Record(AuditEventTypes.UserDeactivated, AuditTargetTypes.User, user.Id,
            before: before, after: new { status = user.Status.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Update the current user's notification preferences (US-M15-006).</summary>
public sealed record UpdateNotificationPreferencesCommand(string? PreferencesJson) : ICommand<Unit>;

public sealed class UpdateNotificationPreferencesCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateNotificationPreferencesCommand, Unit>
{
    public async Task<Unit> Handle(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id)
        {
            throw new UnauthorizedException();
        }

        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);
        user.UpdateNotificationPreferences(command.PreferencesJson);
        audit.Record(AuditEventTypes.NotificationPreferencesUpdated, AuditTargetTypes.User, user.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Grant a role to a user, optionally scoped to a resource (US-M1-017).</summary>
public sealed record GrantRoleCommand(Guid UserId, Guid RoleId, string? ScopeValue) : ICommand<UserRoleDto>;

public sealed class GrantRoleCommandValidator : AbstractValidator<GrantRoleCommand>
{
    public GrantRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class GrantRoleCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<GrantRoleCommand, UserRoleDto>
{
    public async Task<UserRoleDto> Handle(GrantRoleCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);
        var role = await roles.GetByIdAsync(command.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role", command.RoleId);

        if (role.IsArchived)
        {
            throw new ConflictException("role_archived", "Cannot grant an archived role.");
        }

        if (await userRoles.ExistsAsync(user.Id, role.Id, command.ScopeValue, cancellationToken))
        {
            throw new ConflictException("role_already_granted", "The user already holds this role at this scope.");
        }

        var assignment = UserRole.Grant(user.Id, role.Id, command.ScopeValue);
        userRoles.Add(assignment);
        user.MarkActiveOnFirstRole();

        audit.Record(AuditEventTypes.RoleGranted, AuditTargetTypes.UserRole, assignment.Id,
            payload: new { userId = user.Id, roleId = role.Id, role.Name, scopeValue = command.ScopeValue, grantedBy = currentUser.UserId });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAsync(user.Id, cancellationToken);

        return new UserRoleDto(assignment.Id, role.Id, role.Name, assignment.ScopeValue, false, null, null, assignment.IsActive);
    }
}

/// <summary>Revoke a role assignment from a user (US-M1-018).</summary>
public sealed record RevokeRoleCommand(Guid UserId, Guid UserRoleId) : ICommand<Unit>;

public sealed class RevokeRoleCommandHandler(
    IUserRoleRepository userRoles,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeRoleCommand, Unit>
{
    public async Task<Unit> Handle(RevokeRoleCommand command, CancellationToken cancellationToken)
    {
        var assignment = await userRoles.GetByIdAsync(command.UserRoleId, cancellationToken)
            ?? throw new NotFoundException("Role assignment", command.UserRoleId);

        if (assignment.UserId != command.UserId)
        {
            throw new NotFoundException("Role assignment", command.UserRoleId);
        }

        userRoles.Remove(assignment);
        audit.Record(AuditEventTypes.RoleRevoked, AuditTargetTypes.UserRole, assignment.Id,
            payload: new { userId = assignment.UserId, roleId = assignment.RoleId, revokedBy = currentUser.UserId });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAsync(assignment.UserId, cancellationToken);
        return Unit.Value;
    }
}
