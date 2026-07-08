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

/// <summary>The identity provider AuditX authenticates against.</summary>
public enum IdentityProviderKind
{
    /// <summary>Seeded users, no domain required (local development / demo).</summary>
    Development,

    /// <summary>Active Directory over LDAPS (bind for credential validation, service-account lookup).</summary>
    ActiveDirectory,

    /// <summary>Active Directory fronted by the bank's REST gateway (base URL + credential-validation endpoint).</summary>
    ActiveDirectoryApi,
}

/// <summary>Selects and configures the identity provider (bound from <c>Identity</c>).</summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// One of <c>Development</c>, <c>ActiveDirectory</c> (LDAPS), or <c>ActiveDirectoryApi</c> (the bank's AD-over-REST
    /// gateway). Many banks expose AD only through an internal REST API rather than raw LDAP connection details, so
    /// the API provider is selectable alongside LDAP without any AD schema change.
    /// </summary>
    public string Provider { get; set; } = "Development";

    /// <summary>The parsed provider kind; unknown values fall back to <see cref="IdentityProviderKind.Development"/>.</summary>
    public IdentityProviderKind Kind =>
        Enum.TryParse<IdentityProviderKind>(Provider, ignoreCase: true, out var kind) ? kind : IdentityProviderKind.Development;

    public bool UseDevelopmentProvider => Kind == IdentityProviderKind.Development;

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

/// <summary>
/// Configuration for the generic Active-Directory-over-REST identity provider (bound from <c>ActiveDirectoryApi</c>).
/// The bank fronts its directory with an HTTP gateway; AuditX only needs its base URL and a credential-validation
/// endpoint that returns the directory user plus their AD groups. See <c>docs/identity-ad-rest-contract.md</c> for
/// the request/response contract. Path templates may contain <c>{account}</c> or <c>{sid}</c> placeholders.
/// </summary>
public sealed class ActiveDirectoryApiOptions
{
    public const string SectionName = "ActiveDirectoryApi";

    /// <summary>Base URL of the bank's AD gateway, e.g. <c>https://ad-gateway.bank.internal/api/v1</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>POST endpoint that validates <c>{ username, password }</c> and returns the directory user (200) or 401.</summary>
    public string AuthenticatePath { get; set; } = "/authenticate";

    /// <summary>GET endpoint returning a directory user by account name; <c>{account}</c> is URL-encoded in.</summary>
    public string LookupPath { get; set; } = "/users/{account}";

    /// <summary>GET endpoint returning a directory user (or at least <c>enabled</c>) by objectSid; <c>{sid}</c> substituted in.</summary>
    public string StatusPath { get; set; } = "/users/by-sid/{sid}";

    /// <summary>Header carrying the service credential for the gateway (e.g. <c>X-Api-Key</c>). Omit to send none.</summary>
    public string? ApiKeyHeader { get; set; } = "X-Api-Key";

    /// <summary>The service credential value sent in <see cref="ApiKeyHeader"/>. MUST be set per deployment when used.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>Redis connection configuration (bound from <c>Redis</c>).</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";

    public string InstanceName { get; set; } = "auditx:";
}
