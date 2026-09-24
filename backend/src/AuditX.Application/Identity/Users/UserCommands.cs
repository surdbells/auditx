using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Organization;
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

/// <summary>Set (or clear) a user's annual audit capacity in person-days, for workload-vs-capacity planning.</summary>
public sealed record SetUserCapacityCommand(Guid UserId, decimal? CapacityDays) : ICommand<Unit>;

public sealed class SetUserCapacityCommandValidator : AbstractValidator<SetUserCapacityCommand>
{
    public SetUserCapacityCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        // Domain also enforces this; validating here yields a clean 422 instead of a domain exception.
        RuleFor(x => x.CapacityDays).InclusiveBetween(0m, 366m).When(x => x.CapacityDays is not null);
    }
}

public sealed class SetUserCapacityCommandHandler(
    IUserRepository users,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetUserCapacityCommand, Unit>
{
    public async Task<Unit> Handle(SetUserCapacityCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        var before = new { capacityDays = user.CapacityDays };
        user.SetCapacityDays(command.CapacityDays);
        audit.Record(AuditEventTypes.UserCapacityUpdated, AuditTargetTypes.User, user.Id,
            before: before, after: new { capacityDays = user.CapacityDays });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Set (or clear) a user's explicit line manager, for reporting-line workflows.</summary>
public sealed record SetUserManagerCommand(Guid UserId, Guid? ManagerId) : ICommand<Unit>;

public sealed class SetUserManagerCommandValidator : AbstractValidator<SetUserManagerCommand>
{
    public SetUserManagerCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x).Must(x => x.ManagerId != x.UserId).WithMessage("A user cannot be their own line manager.");
    }
}

public sealed class SetUserManagerCommandHandler(
    IUserRepository users,
    IReportingLineResolver reportingLine,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetUserManagerCommand, Unit>
{
    public async Task<Unit> Handle(SetUserManagerCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        if (command.ManagerId is { } managerId)
        {
            _ = await users.GetByIdAsync(managerId, cancellationToken)
                ?? throw new NotFoundException("User", managerId);

            // Reject cycles: the proposed manager must not already report to this user (directly, or via org-unit heads).
            var managerChain = await reportingLine.GetReportingChainAsync(managerId, cancellationToken);
            if (managerChain.Contains(command.UserId))
            {
                throw new ConflictException("user.manager_cycle", "That would create a cycle in the reporting line.");
            }
        }

        var before = new { managerId = user.ManagerId };
        user.SetManager(command.ManagerId);
        audit.Record(AuditEventTypes.UserManagerUpdated, AuditTargetTypes.User, user.Id,
            before: before, after: new { managerId = user.ManagerId });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>A resolved reporting line: the effective line manager plus the ordered chain upward (with names).</summary>
public sealed record ReportingLineDto(Guid UserId, Guid? LineManagerId, IReadOnlyList<ReportingLineNodeDto> Chain);

public sealed record ReportingLineNodeDto(Guid UserId, string DisplayName);

/// <summary>Resolve a user's effective reporting line (explicit manager, else org-unit head walking up the tree).</summary>
public sealed record GetUserReportingLineQuery(Guid UserId) : IQuery<ReportingLineDto>;

public sealed class GetUserReportingLineQueryHandler(IUserRepository users, IReportingLineResolver reportingLine)
    : IQueryHandler<GetUserReportingLineQuery, ReportingLineDto>
{
    public async Task<ReportingLineDto> Handle(GetUserReportingLineQuery query, CancellationToken cancellationToken)
    {
        _ = await users.GetByIdAsync(query.UserId, cancellationToken) ?? throw new NotFoundException("User", query.UserId);

        var chainIds = await reportingLine.GetReportingChainAsync(query.UserId, cancellationToken);
        var entities = chainIds.Count == 0 ? [] : await users.GetByIdsAsync(chainIds, cancellationToken);
        var nameById = entities.ToDictionary(u => u.Id, u => u.DisplayName);

        var chain = chainIds
            .Select(id => new ReportingLineNodeDto(id, nameById.TryGetValue(id, out var n) ? n : "(unknown)"))
            .ToArray();

        return new ReportingLineDto(query.UserId, chainIds.Count > 0 ? chainIds[0] : null, chain);
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

/// <summary>The current user's notification preferences (raw JSON; null when never set).</summary>
public sealed record NotificationPreferencesDto(string? PreferencesJson);

/// <summary>Read the current user's notification preferences (US-M15-006 / M10).</summary>
public sealed record GetMyNotificationPreferencesQuery : IQuery<NotificationPreferencesDto>;

public sealed class GetMyNotificationPreferencesQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IQueryHandler<GetMyNotificationPreferencesQuery, NotificationPreferencesDto>
{
    public async Task<NotificationPreferencesDto> Handle(GetMyNotificationPreferencesQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id)
        {
            throw new UnauthorizedException();
        }

        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);
        return new NotificationPreferencesDto(user.NotificationPreferencesJson);
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
