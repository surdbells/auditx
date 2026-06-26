namespace AuditX.Infrastructure.Options;

/// <summary>Session-JWT signing and lifetime configuration (bound from <c>Jwt</c>).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA-256 signing key. MUST be overridden per deployment (≥ 32 bytes).</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "auditx";

    public string Audience { get; set; } = "auditx";

    /// <summary>Sliding lifetime in minutes (default 8 hours).</summary>
    public int SlidingMinutes { get; set; } = 480;

    /// <summary>Absolute, non-extendable lifetime in minutes (default 24 hours).</summary>
    public int AbsoluteMinutes { get; set; } = 1440;
}

/// <summary>Selects and configures the identity provider (bound from <c>Identity</c>).</summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary><c>ActiveDirectory</c> (production) or <c>Development</c> (seeded users, no domain required).</summary>
    public string Provider { get; set; } = "Development";

    public bool UseDevelopmentProvider => string.Equals(Provider, "Development", StringComparison.OrdinalIgnoreCase);

    /// <summary>Interval, in minutes, between AD account-status re-verifications on session refresh (BR-M1-011).</summary>
    public int AdReverificationMinutes { get; set; } = 5;
}

/// <summary>LDAPS / Kerberos configuration for the Active Directory identity provider (bound from <c>ActiveDirectory</c>).</summary>
public sealed class ActiveDirectoryOptions
{
    public const string SectionName = "ActiveDirectory";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 636;

    /// <summary>LDAPS is mandatory; plaintext LDAP is not supported.</summary>
    public bool UseLdaps { get; set; } = true;

    public string BaseDn { get; set; } = string.Empty;

    public string ServiceAccountDn { get; set; } = string.Empty;

    public string ServiceAccountPassword { get; set; } = string.Empty;

    /// <summary>UPN suffix used to build a bind DN from a bare sAMAccountName (e.g. <c>bank.local</c>).</summary>
    public string? UpnSuffix { get; set; }
}

/// <summary>Redis connection configuration (bound from <c>Redis</c>).</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";

    public string InstanceName { get; set; } = "auditx:";
}
