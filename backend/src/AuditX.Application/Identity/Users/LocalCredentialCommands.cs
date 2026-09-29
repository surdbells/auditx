using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Passwords;
using AuditX.Application.Identity.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Identity.Users;

/// <summary>Result of an admin credential action; <see cref="GeneratedPassword"/> is set only for the generate-temp method.</summary>
public sealed record LocalCredentialResultDto(Guid UserId, string Username, string? GeneratedPassword);

internal static class UsernameRules
{
    public static void Apply<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string>> selector)
    {
        validator.RuleFor(selector).NotEmpty().MinimumLength(3).MaximumLength(256)
            .Matches("^[A-Za-z0-9._@-]+$")
            .WithMessage("Username may contain only letters, digits and . _ @ -");
    }
}

// ---- Create a local user (email/name/username + initial password) ----

public sealed record CreateLocalUserCommand(
    string Email, string FirstName, string LastName, string Username,
    IReadOnlyList<string>? RoleNames, InitialPasswordMethod Method, string? Password) : ICommand<LocalCredentialResultDto>;

public sealed class CreateLocalUserCommandValidator : AbstractValidator<CreateLocalUserCommand>
{
    public CreateLocalUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.FirstName).MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
        UsernameRules.Apply(this, x => x.Username);
    }
}

public sealed class CreateLocalUserCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IInstitutionSettingsRepository institutionSettings,
    IPermissionResolver permissions,
    LocalCredentialAdminService credentialAdmin,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateLocalUserCommand, LocalCredentialResultDto>
{
    public async Task<LocalCredentialResultDto> Handle(CreateLocalUserCommand command, CancellationToken cancellationToken)
    {
        var username = command.Username.Trim();
        if (await users.ExistsByUsernameAsync(username, cancellationToken))
        {
            throw new ConflictException("user.username_taken", $"The username '{username}' is already in use.");
        }

        var resolved = new List<Role>();
        foreach (var roleName in command.RoleNames ?? [])
        {
            resolved.Add(await roles.GetByNameAsync(roleName, cancellationToken) ?? throw new NotFoundException("Role", roleName));
        }

        var settings = await institutionSettings.GetAsync(cancellationToken);
        var user = User.CreateLocal(username, command.Email.Trim(), command.FirstName, command.LastName);
        users.Add(user);
        foreach (var role in resolved)
        {
            userRoles.Add(UserRole.Grant(user.Id, role.Id));
            user.MarkActiveOnFirstRole();
        }

        var generated = await credentialAdmin.ApplyInitialAsync(user, command.Method, command.Password, settings, cancellationToken);

        audit.Record(AuditEventTypes.UserProvisioned, AuditTargetTypes.User, user.Id,
            after: new { user.Email, user.Username, source = user.AuthenticationSource.ToString(), roles = resolved.Select(r => r.Name).ToArray() });
        audit.Record(AuditEventTypes.LocalCredentialSet, AuditTargetTypes.User, user.Id, after: new { method = command.Method.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (resolved.Count > 0)
        {
            await permissions.InvalidateAsync(user.Id, cancellationToken);
        }

        return new LocalCredentialResultDto(user.Id, username, generated);
    }
}

// ---- Enable a local credential on an existing user ----

public sealed record EnableLocalCredentialCommand(Guid UserId, string Username, InitialPasswordMethod Method, string? Password) : ICommand<LocalCredentialResultDto>;

public sealed class EnableLocalCredentialCommandValidator : AbstractValidator<EnableLocalCredentialCommand>
{
    public EnableLocalCredentialCommandValidator() => UsernameRules.Apply(this, x => x.Username);
}

public sealed class EnableLocalCredentialCommandHandler(
    IUserRepository users,
    IInstitutionSettingsRepository institutionSettings,
    LocalCredentialAdminService credentialAdmin,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<EnableLocalCredentialCommand, LocalCredentialResultDto>
{
    public async Task<LocalCredentialResultDto> Handle(EnableLocalCredentialCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken) ?? throw new NotFoundException("User", command.UserId);
        var username = command.Username.Trim();

        var holder = await users.GetByUsernameAsync(username, cancellationToken);
        if (holder is not null && holder.Id != user.Id)
        {
            throw new ConflictException("user.username_taken", $"The username '{username}' is already in use.");
        }

        var settings = await institutionSettings.GetAsync(cancellationToken);
        user.EnableLocalAuthentication(username);
        var generated = await credentialAdmin.ApplyInitialAsync(user, command.Method, command.Password, settings, cancellationToken);

        audit.Record(AuditEventTypes.LocalCredentialSet, AuditTargetTypes.User, user.Id,
            after: new { user.Username, method = command.Method.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LocalCredentialResultDto(user.Id, username, generated);
    }
}

// ---- Admin reset of an existing local user's password ----

public sealed record AdminResetLocalPasswordCommand(Guid UserId, InitialPasswordMethod Method, string? Password) : ICommand<LocalCredentialResultDto>;

public sealed class AdminResetLocalPasswordCommandHandler(
    IUserRepository users,
    IInstitutionSettingsRepository institutionSettings,
    LocalCredentialAdminService credentialAdmin,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AdminResetLocalPasswordCommand, LocalCredentialResultDto>
{
    public async Task<LocalCredentialResultDto> Handle(AdminResetLocalPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken) ?? throw new NotFoundException("User", command.UserId);
        if (user.AuthenticationSource != Domain.Enums.AuthenticationSource.Local || user.Username is null)
        {
            throw new ConflictException("user.not_local", "This user does not use a local password.");
        }

        var settings = await institutionSettings.GetAsync(cancellationToken);
        var generated = await credentialAdmin.ApplyInitialAsync(user, command.Method, command.Password, settings, cancellationToken);

        audit.Record(AuditEventTypes.PasswordReset, AuditTargetTypes.User, user.Id, after: new { method = command.Method.ToString(), by = "admin" });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LocalCredentialResultDto(user.Id, user.Username, generated);
    }
}

// ---- Unlock a locked-out account ----

public sealed record UnlockUserCommand(Guid UserId) : ICommand<Unit>;

public sealed class UnlockUserCommandHandler(
    IUserCredentialRepository credentials,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UnlockUserCommand, Unit>
{
    public async Task<Unit> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        var credential = await credentials.GetByUserIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("Credential", command.UserId);
        credential.Unlock();
        audit.Record(AuditEventTypes.AccountUnlocked, AuditTargetTypes.User, command.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
