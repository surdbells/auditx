using System.Security.Claims;
using System.Text;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AuditX.Infrastructure.Identity;

/// <summary>
/// Issues HMAC-SHA-256 signed session JWTs with a sliding expiry capped by a non-extendable absolute
/// lifetime. The absolute expiry is carried in a custom <c>abs_exp</c> claim so refreshes can slide the
/// token without ever extending its absolute lifetime (US-M1-007).
/// </summary>
public sealed class SessionTokenService : ISessionTokenService
{
    public const string AbsoluteExpiryClaim = "abs_exp";

    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signingCredentials;

    public SessionTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;

        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes for HMAC-SHA-256.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken Issue(Guid userId, IReadOnlyCollection<string> roleNames)
    {
        var absolute = _clock.UtcNow.AddMinutes(_options.AbsoluteMinutes);
        return Create(userId, roleNames, absolute);
    }

    public IssuedToken Refresh(Guid userId, IReadOnlyCollection<string> roleNames, DateTimeOffset absoluteExpiresAt)
        => Create(userId, roleNames, absoluteExpiresAt);

    private IssuedToken Create(Guid userId, IReadOnlyCollection<string> roleNames, DateTimeOffset absoluteExpiresAt)
    {
        var now = _clock.UtcNow;
        var sliding = now.AddMinutes(_options.SlidingMinutes);
        var expires = sliding < absoluteExpiresAt ? sliding : absoluteExpiresAt;
        var tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(AbsoluteExpiryClaim, absoluteExpiresAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };
        claims.AddRange(roleNames.Select(role => new Claim(ClaimTypes.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _signingCredentials,
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedToken(token, tokenId, expires, absoluteExpiresAt);
    }
}
