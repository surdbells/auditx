using System.Security.Claims;
using AuditX.Application.Abstractions;
using AuditX.Infrastructure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AuditX.Api.Identity;

/// <summary>Resolves the current principal from the validated session JWT on the ambient HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? SessionTokenId => Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);

    public DateTimeOffset? SessionExpiresAt => ReadUnix(JwtRegisteredClaimNames.Exp);

    public DateTimeOffset? SessionAbsoluteExpiresAt => ReadUnix(SessionTokenService.AbsoluteExpiryClaim);

    private DateTimeOffset? ReadUnix(string claimType)
        => long.TryParse(Principal?.FindFirstValue(claimType), out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
