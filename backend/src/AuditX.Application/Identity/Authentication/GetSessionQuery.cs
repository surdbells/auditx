using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Identity.Authentication;

/// <summary>
/// Returns the current session and, on each call (a session refresh), re-verifies that the user's AD
/// account is still enabled. If the AD account has been disabled/removed, the session is revoked and
/// the request is rejected (US-M1-008, BR-M1-011).
/// </summary>
public sealed record GetSessionQuery : IQuery<SessionDto>;

public sealed class GetSessionQueryHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IIdentityProvider identityProvider,
    ITokenDenylist denylist,
    AuthSessionService sessionService,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork,
    IClock clock)
    : IQueryHandler<GetSessionQuery, SessionDto>
{
    public async Task<SessionDto> Handle(GetSessionQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException();
        }

        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("unknown_user", "Session user no longer exists.");

        if (user.Status == UserStatus.Deactivated || !await identityProvider.IsAccountEnabledAsync(user.AdObjectSid, cancellationToken))
        {
            await RevokeAndAuditAsync(user.Id, cancellationToken);
            throw new UnauthorizedException("session_terminated", "Your session has been terminated.");
        }

        var roleNames = await sessionService.GetActiveRoleNamesAsync(user.Id, cancellationToken);
        var expiresAt = currentUser.SessionExpiresAt ?? clock.UtcNow;
        var absoluteExpiresAt = currentUser.SessionAbsoluteExpiresAt ?? clock.UtcNow;

        return await sessionService.BuildSessionAsync(user, roleNames, expiresAt, absoluteExpiresAt, cancellationToken);
    }

    private async Task RevokeAndAuditAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (currentUser.SessionTokenId is { } tokenId)
        {
            await denylist.RevokeAsync(tokenId, currentUser.SessionAbsoluteExpiresAt ?? clock.UtcNow.AddHours(24), cancellationToken);
        }

        audit.RecordAs(ActorType.System, "ad-reverification", userId,
            AuditEventTypes.SessionTerminated, AuditTargetTypes.Session, userId,
            payload: new { reason = "ad_account_disabled" });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
