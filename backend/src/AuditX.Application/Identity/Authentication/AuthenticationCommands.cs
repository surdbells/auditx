using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Identity.Authentication;

/// <summary>Forms-fallback authentication: credentials validated by an LDAP bind (US-M1-003).</summary>
public sealed record LoginCommand(string Username, string Password) : ICommand<AuthResultDto>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    IIdentityProvider identityProvider,
    IUserRepository users,
    LocalAuthenticator localAuthenticator,
    AuthSessionService sessionService,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // Local-first: if the username matches a local-password user, authenticate against the stored hash.
        // This gives per-user hybrid auth (some local, some directory) regardless of the configured provider.
        var localUser = await users.GetByUsernameAsync(command.Username, cancellationToken);
        if (localUser is { AuthenticationSource: AuthenticationSource.Local })
        {
            return await localAuthenticator.AuthenticateAsync(localUser, command.Password, cancellationToken);
        }

        var directoryUser = await identityProvider.AuthenticateAsync(command.Username, command.Password, cancellationToken);
        if (directoryUser is null)
        {
            // Record the failed attempt, then fail opaquely (no account-existence leak).
            audit.RecordAs(ActorType.System, "anonymous", null, AuditEventTypes.LoginFailed, AuditTargetTypes.Session, null,
                payload: new { username = command.Username, reason = "invalid_credentials" });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("invalid_credentials", "Invalid username or password.");
        }

        if (!directoryUser.IsEnabled)
        {
            throw new UnauthorizedException("account_disabled", "Invalid username or password.");
        }

        return await sessionService.CreateSessionAsync(directoryUser, AuthenticationMethod.Forms, cancellationToken);
    }
}

/// <summary>Kerberos/IWA SSO: the account name is resolved from the validated Negotiate identity (US-M1-002).</summary>
public sealed record SsoLoginCommand(string AccountName) : ICommand<AuthResultDto>;

public sealed class SsoLoginCommandHandler(
    IIdentityProvider identityProvider,
    AuthSessionService sessionService)
    : ICommandHandler<SsoLoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(SsoLoginCommand command, CancellationToken cancellationToken)
    {
        var directoryUser = await identityProvider.FindByAccountNameAsync(command.AccountName, cancellationToken)
            ?? throw new UnauthorizedException("sso_unresolved", "Your directory account could not be resolved.");

        if (!directoryUser.IsEnabled)
        {
            throw new UnauthorizedException("account_disabled", "Your account is not enabled.");
        }

        return await sessionService.CreateSessionAsync(directoryUser, AuthenticationMethod.Kerberos, cancellationToken);
    }
}

/// <summary>Logout: revokes the current session token via the server-side denylist (US-M1-009).</summary>
public sealed record LogoutCommand : ICommand<Unit>;

public sealed class LogoutCommandHandler(
    ICurrentUser currentUser,
    ITokenDenylist denylist,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.SessionTokenId is { } tokenId)
        {
            var expiry = currentUser.SessionAbsoluteExpiresAt ?? clock.UtcNow.AddHours(24);
            await denylist.RevokeAsync(tokenId, expiry, cancellationToken);
            audit.Record(AuditEventTypes.LoggedOut, AuditTargetTypes.Session, currentUser.UserId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
