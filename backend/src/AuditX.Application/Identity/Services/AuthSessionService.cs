using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Identity.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.Services;

/// <summary>
/// Shared logic for turning an AD-authenticated <see cref="DirectoryUser"/> into an AuditX session:
/// just-in-time provisioning, deactivation checks, login recording, token issuance and session
/// assembly. Used by both the forms-login and Kerberos/SSO handlers so the two paths behave
/// identically (US-M1-002, US-M1-003, US-M1-005).
/// </summary>
public sealed class AuthSessionService(
    IUserRepository users,
    IUserRoleRepository userRoles,
    IRoleRepository roles,
    IInstitutionSettingsRepository institutionSettings,
    IIdentityProvider identityProvider,
    ISessionTokenService tokenService,
    IPermissionResolver permissions,
    IUserCredentialRepository credentials,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthResultDto> CreateSessionAsync(DirectoryUser directoryUser, AuthenticationMethod method, CancellationToken cancellationToken)
    {
        var user = await users.GetByObjectSidAsync(directoryUser.ObjectSid, cancellationToken);

        if (user is null)
        {
            var settings = await institutionSettings.GetAsync(cancellationToken);
            var permitted = await identityProvider.IsPermittedToProvisionAsync(
                directoryUser, settings.AdProvisioningFilterOuDn, settings.AdProvisioningFilterGroupSid, cancellationToken);
            if (!permitted)
            {
                throw new UnauthorizedException("provisioning_denied", "Your account is not permitted to access AuditX.");
            }

            user = User.ProvisionFromDirectory(
                directoryUser.SamAccountName,
                directoryUser.UserPrincipalName,
                directoryUser.ObjectSid,
                directoryUser.Email,
                directoryUser.FirstName,
                directoryUser.LastName,
                directoryUser.DisplayName);
            users.Add(user);
            audit.Record(AuditEventTypes.UserProvisioned, AuditTargetTypes.User, user.Id,
                after: new { user.AdSamAccountName, user.Email, status = user.Status.ToString() });
        }
        else
        {
            if (user.Status == UserStatus.Deactivated)
            {
                throw new UnauthorizedException("account_deactivated", "Your account is deactivated.");
            }

            user.RefreshDirectoryAttributes(directoryUser.Email, directoryUser.FirstName, directoryUser.LastName, directoryUser.DisplayName);
        }

        user.RecordLogin(clock.UtcNow, method);

        var roleNames = await GetActiveRoleNamesAsync(user.Id, cancellationToken);
        var token = tokenService.Issue(user.Id, roleNames);

        audit.Record(AuditEventTypes.LoginSucceeded, AuditTargetTypes.Session, user.Id,
            payload: new { method = method.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var session = await BuildSessionAsync(user, roleNames, token.ExpiresAt, token.AbsoluteExpiresAt, cancellationToken);
        return new AuthResultDto(token.Token, token.ExpiresAt, token.AbsoluteExpiresAt, session);
    }

    public async Task<SessionDto> BuildSessionAsync(
        User user,
        IReadOnlyList<string> roleNames,
        DateTimeOffset expiresAt,
        DateTimeOffset absoluteExpiresAt,
        CancellationToken cancellationToken)
    {
        var effective = await permissions.GetEffectivePermissionsAsync(user.Id, cancellationToken);
        var permissionKeys = effective.Select(p => p.Key).Distinct().OrderBy(k => k).ToArray();

        var mustChangePassword = false;
        if (user.AuthenticationSource == AuthenticationSource.Local)
        {
            var credential = await credentials.GetByUserIdAsync(user.Id, cancellationToken);
            mustChangePassword = credential?.MustChangePassword ?? false;
        }

        return new SessionDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.DisplayName,
            Common.Enums.EnumExtensions.ToSnake(user.Status), roleNames, permissionKeys, expiresAt, absoluteExpiresAt, mustChangePassword);
    }

    public async Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var assignments = await userRoles.GetForUserAsync(userId, cancellationToken);
        var activeRoleIds = assignments.Where(a => a.IsEffectiveAt(now)).Select(a => a.RoleId).Distinct().ToArray();
        if (activeRoleIds.Length == 0)
        {
            return [];
        }

        var roleEntities = await roles.GetByIdsAsync(activeRoleIds, cancellationToken);
        return roleEntities.Select(r => r.Name).OrderBy(n => n).ToArray();
    }
}
