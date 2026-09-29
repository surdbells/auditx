using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Identity.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.Services;

/// <summary>
/// Authenticates a local-password user: enforces the master toggle, lockout and expiry, verifies the password
/// against the stored PBKDF2 hash (constant-time), tracks failed attempts, transparently upgrades weak hashes,
/// and issues a session whose token carries the credential security stamp (so a later change invalidates it).
/// Failures are opaque (no account-existence leak) except the actionable <c>account_locked</c>.
/// </summary>
public sealed class LocalAuthenticator(
    IInstitutionSettingsRepository institutionSettings,
    IUserCredentialRepository credentials,
    IPasswordHasher passwordHasher,
    ISessionTokenService tokenService,
    AuthSessionService sessionService,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthResultDto> AuthenticateAsync(User user, string password, CancellationToken cancellationToken)
    {
        var settings = await institutionSettings.GetAsync(cancellationToken);
        var now = clock.UtcNow;

        // The master toggle governs whether local passwords may be used at all.
        if (!settings.EnableLocalPasswords)
        {
            await RecordFailureAsync(user.Id, "local_disabled", cancellationToken);
            throw new UnauthorizedException("invalid_credentials", "Invalid username or password.");
        }

        if (user.Status == UserStatus.Deactivated)
        {
            throw new UnauthorizedException("account_deactivated", "Your account is deactivated.");
        }

        var credential = await credentials.GetByUserIdAsync(user.Id, cancellationToken);
        if (credential is null)
        {
            await RecordFailureAsync(user.Id, "no_credential", cancellationToken);
            throw new UnauthorizedException("invalid_credentials", "Invalid username or password.");
        }

        if (credential.IsLockedOut(now))
        {
            throw new UnauthorizedException("account_locked", "Your account is temporarily locked. Please try again later.");
        }

        var verification = passwordHasher.Verify(credential.PasswordHash, password);
        if (!verification.Succeeded)
        {
            credential.RegisterFailedAttempt(settings.PasswordMaxFailedAttempts, settings.PasswordLockoutMinutes, now);
            audit.RecordAs(ActorType.System, "anonymous", user.Id, AuditEventTypes.LoginFailed, AuditTargetTypes.Session, user.Id,
                payload: new { reason = "invalid_credentials", method = AuthenticationMethod.LocalPassword.ToString() });
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (credential.IsLockedOut(now))
            {
                throw new UnauthorizedException("account_locked", "Your account is temporarily locked. Please try again later.");
            }

            throw new UnauthorizedException("invalid_credentials", "Invalid username or password.");
        }

        // Successful verification.
        if (verification.NeedsRehash)
        {
            credential.UpgradeHash(passwordHasher.Hash(password));
        }

        credential.RegisterSuccessfulLogin();

        // An expired password forces a change on this sign-in (surfaced via the session's must-change flag).
        if (credential.IsExpired(settings.PasswordExpiryDays, now))
        {
            credential.RequireChange();
        }

        user.RecordLogin(now, AuthenticationMethod.LocalPassword);

        var roleNames = await sessionService.GetActiveRoleNamesAsync(user.Id, cancellationToken);
        var token = tokenService.Issue(user.Id, roleNames, credential.SecurityStamp);

        audit.Record(AuditEventTypes.LoginSucceeded, AuditTargetTypes.Session, user.Id,
            payload: new { method = AuthenticationMethod.LocalPassword.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var session = await sessionService.BuildSessionAsync(user, roleNames, token.ExpiresAt, token.AbsoluteExpiresAt, cancellationToken);
        return new AuthResultDto(token.Token, token.ExpiresAt, token.AbsoluteExpiresAt, session);
    }

    private async Task RecordFailureAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        audit.RecordAs(ActorType.System, "anonymous", userId, AuditEventTypes.LoginFailed, AuditTargetTypes.Session, userId,
            payload: new { reason, method = AuthenticationMethod.LocalPassword.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
