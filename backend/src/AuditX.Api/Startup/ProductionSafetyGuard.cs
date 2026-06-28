using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AuditX.Api.Startup;

/// <summary>
/// Fail-fast production-safety checks run at startup. Outside the Development environment the app refuses to boot if it
/// would run with an insecure configuration: the seeded Development identity provider, a missing/placeholder JWT
/// signing key, or no database connection string. This turns the audit's "must never be enabled in production"
/// doc-comments into an enforced invariant (CRITICAL findings: dev-provider-in-prod, committed signing key).
/// </summary>
public static class ProductionSafetyGuard
{
    private const string PlaceholderKeyPrefix = "CHANGE-ME";
    private const int MinSigningKeyBytes = 32;

    public static void Validate(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var identity = services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        if (identity.UseDevelopmentProvider)
        {
            throw new InvalidOperationException(
                "Identity:Provider is 'Development' outside the Development environment. The seeded development "
                + "identity provider (well-known accounts) must never run in production — set Identity:Provider to "
                + "'ActiveDirectory'.");
        }

        var jwt = services.GetRequiredService<IOptions<JwtOptions>>().Value;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey)
            || jwt.SigningKey.StartsWith(PlaceholderKeyPrefix, StringComparison.OrdinalIgnoreCase)
            || System.Text.Encoding.UTF8.GetByteCount(jwt.SigningKey) < MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing, the committed placeholder, or shorter than 32 bytes. Supply a strong "
                + "per-deployment signing key from a secret store before starting in a non-Development environment.");
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Supply the database connection string per deployment.");
        }
    }
}
