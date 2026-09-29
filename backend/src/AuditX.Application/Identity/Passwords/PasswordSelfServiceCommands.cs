using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Identity.Passwords;

// ---- Change own password (authenticated) ----

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<AuthResultDto>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

public sealed class ChangePasswordCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IInstitutionSettingsRepository institutionSettings,
    IUserCredentialRepository credentials,
    IPasswordHasher passwordHasher,
    PasswordService passwordService,
    AuthSessionService sessionService,
    ISessionTokenService tokenService,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangePasswordCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("unauthenticated", "You are not signed in.");
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("unauthenticated", "You are not signed in.");

        if (user.AuthenticationSource != AuthenticationSource.Local)
        {
            throw new ForbiddenAccessException("This account does not use a local password.");
        }

        var settings = await institutionSettings.GetAsync(cancellationToken);
        var credential = await credentials.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new ForbiddenAccessException("This account has no local password set.");

        if (!passwordHasher.Verify(credential.PasswordHash, command.CurrentPassword).Succeeded)
        {
            throw new ValidationException([new ValidationFailure("currentPassword", "Your current password is incorrect.")]);
        }

        await passwordService.ValidateAsync(userId, command.NewPassword, settings, cancellationToken);
        var updated = await passwordService.ApplyAsync(userId, command.NewPassword, mustChange: false, settings, cancellationToken);

        audit.Record(AuditEventTypes.PasswordChanged, AuditTargetTypes.User, userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Issue a fresh session carrying the new stamp; every other outstanding session is now invalid.
        var roleNames = await sessionService.GetActiveRoleNamesAsync(userId, cancellationToken);
        var token = tokenService.Issue(userId, roleNames, updated.SecurityStamp);
        var session = await sessionService.BuildSessionAsync(user, roleNames, token.ExpiresAt, token.AbsoluteExpiresAt, cancellationToken);
        return new AuthResultDto(token.Token, token.ExpiresAt, token.AbsoluteExpiresAt, session);
    }
}

// ---- Forgot password (anonymous, no user enumeration) ----

public sealed record ForgotPasswordCommand(string UsernameOrEmail) : ICommand<Unit>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(x => x.UsernameOrEmail).NotEmpty().MaximumLength(320);
}

public sealed class ForgotPasswordCommandHandler(
    IUserRepository users,
    IInstitutionSettingsRepository institutionSettings,
    IPasswordResetTokenRepository tokens,
    IEmailSender emailSender,
    IAppUrlProvider appUrls,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<ForgotPasswordCommandHandler> logger)
    : ICommandHandler<ForgotPasswordCommand, Unit>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<Unit> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var settings = await institutionSettings.GetAsync(cancellationToken);
        if (!settings.EnableLocalPasswords)
        {
            return Unit.Value; // Local passwords disabled — behave identically to "no such account".
        }

        var identifier = command.UsernameOrEmail.Trim();
        var user = await users.GetByUsernameAsync(identifier, cancellationToken)
            ?? await users.GetByEmailAsync(identifier, cancellationToken);

        // Never reveal whether the account exists / is eligible — always return success.
        if (user is not { AuthenticationSource: AuthenticationSource.Local, Status: not UserStatus.Deactivated })
        {
            return Unit.Value;
        }

        var now = clock.UtcNow;
        foreach (var outstanding in await tokens.GetUnconsumedForUserAsync(user.Id, cancellationToken))
        {
            outstanding.Consume(now); // invalidate any earlier outstanding link
        }

        var raw = CredentialTokens.GenerateRawToken();
        tokens.Add(PasswordResetToken.Issue(user.Id, CredentialTokens.Hash(raw), CredentialTokenPurpose.Reset, now.Add(TokenLifetime)));
        audit.RecordAs(ActorType.System, "anonymous", user.Id, AuditEventTypes.PasswordResetRequested, AuditTargetTypes.User, user.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await SendResetEmailAsync(user, raw, cancellationToken);
        return Unit.Value;
    }

    private async Task SendResetEmailAsync(User user, string rawToken, CancellationToken cancellationToken)
    {
        var baseUrl = appUrls.WebBaseUrl?.TrimEnd('/') ?? string.Empty;
        var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var body =
            $"Hello {user.FirstName},\n\n" +
            "We received a request to reset your AuditX password. Use the link below within the next hour to set a new password:\n\n" +
            $"{link}\n\n" +
            "If you did not request this, you can safely ignore this email — your password will not change.";

        try
        {
            var result = await emailSender.SendAsync(user.Email, "Reset your AuditX password", body, cancellationToken);
            if (!result.Success)
            {
                logger.LogWarning("Password-reset email to user {UserId} was not sent: {Error}", user.Id, result.Error);
            }
        }
        catch (Exception ex)
        {
            // Never surface delivery failures to the caller (no enumeration); the token still exists for a retry.
            logger.LogError(ex, "Failed to send password-reset email to user {UserId}", user.Id);
        }
    }
}

// ---- Reset / set password via emailed token (anonymous) ----

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Unit>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

public sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IInstitutionSettingsRepository institutionSettings,
    IPasswordResetTokenRepository tokens,
    PasswordService passwordService,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var token = await tokens.GetByTokenHashAsync(CredentialTokens.Hash(command.Token.Trim()), cancellationToken);
        if (token is null || !token.IsRedeemable(now))
        {
            throw new UnauthorizedException("invalid_or_expired_token", "This link is invalid or has expired.");
        }

        var settings = await institutionSettings.GetAsync(cancellationToken);
        if (!settings.EnableLocalPasswords)
        {
            throw new UnauthorizedException("invalid_or_expired_token", "This link is invalid or has expired.");
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is not { AuthenticationSource: AuthenticationSource.Local })
        {
            throw new UnauthorizedException("invalid_or_expired_token", "This link is invalid or has expired.");
        }

        await passwordService.ValidateAsync(user.Id, command.NewPassword, settings, cancellationToken);
        await passwordService.ApplyAsync(user.Id, command.NewPassword, mustChange: false, settings, cancellationToken);
        token.Consume(now);

        audit.RecordAs(ActorType.System, "anonymous", user.Id, AuditEventTypes.PasswordReset, AuditTargetTypes.User, user.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
