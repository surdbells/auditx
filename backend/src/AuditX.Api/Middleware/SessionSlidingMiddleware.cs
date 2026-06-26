using System.Security.Claims;
using AuditX.Api.Authentication;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;

namespace AuditX.Api.Middleware;

/// <summary>
/// Slides the session on activity: when an authenticated request's token is within the last hour of its
/// sliding window (but still before the absolute expiry), a refreshed token is issued and the cookie
/// reset — never extending the absolute lifetime (US-M1-007). Roles are read from the existing token,
/// so no database access is required.
/// </summary>
public sealed class SessionSlidingMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(60);

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, ISessionTokenService tokenService)
    {
        if (currentUser is { IsAuthenticated: true, UserId: { } userId, SessionTokenId: not null }
            && currentUser.SessionExpiresAt is { } expiresAt
            && currentUser.SessionAbsoluteExpiresAt is { } absoluteExpiresAt)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < absoluteExpiresAt && expiresAt - now < RefreshThreshold)
            {
                var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
                var refreshed = tokenService.Refresh(userId, roles, absoluteExpiresAt);
                SessionCookie.Write(context, refreshed.Token, refreshed.AbsoluteExpiresAt);
            }
        }

        await next(context);
    }
}
