using System.Text;
using AuditX.Application.Abstractions.Identity;
using AuditX.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AuditX.Api.Authentication;

/// <summary>Configures JWT-bearer (session cookie) and Negotiate (Kerberos/IWA) authentication.</summary>
public static class AuthenticationSetup
{
    public const string NegotiateScheme = "Negotiate";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    // The token lives in an HttpOnly cookie; fall back to it when no bearer header is present.
                    OnMessageReceived = ctx =>
                    {
                        if (string.IsNullOrEmpty(ctx.Token) && ctx.Request.Cookies.TryGetValue(SessionCookie.Name, out var cookie))
                        {
                            ctx.Token = cookie;
                        }

                        return Task.CompletedTask;
                    },

                    // Enforce the server-side denylist (logout / force-logout / AD-disablement).
                    OnTokenValidated = async ctx =>
                    {
                        var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        if (!string.IsNullOrEmpty(jti))
                        {
                            var denylist = ctx.HttpContext.RequestServices.GetRequiredService<ITokenDenylist>();
                            if (await denylist.IsRevokedAsync(jti, ctx.HttpContext.RequestAborted))
                            {
                                ctx.Fail("Token has been revoked.");
                            }
                        }
                    },
                };
            })
            .AddNegotiate();

        services.AddAuthorization();
        return services;
    }
}
