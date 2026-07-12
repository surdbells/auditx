namespace AuditX.Application.Abstractions.Administration;

/// <summary>Result of verifying a signed offline release package.</summary>
public sealed record ReleaseVerificationResult(bool IsValid, string Detail);

/// <summary>
/// Verifies a signed offline release package (US-M15-032): the package signature against ITANDT's
/// release-signing public key and the manifest SHA-256. Tampered packages must fail verification.
/// </summary>
public interface IReleasePackageVerifier
{
    ReleaseVerificationResult Verify(string version, string manifestSha256, byte[] signature, byte[] manifestContent);
}

/// <summary>Aggregate deployment metrics + dependency probes for the system-health surface (US-M15-029).</summary>
public sealed record SystemMetrics(
    bool DatabaseConnected,
    long DatabaseLatencyMs,
    bool CacheConnected,
    int ActiveUserCount,
    int TotalUserCount,
    int AuditCount,
    int ExceptionCount,
    int ControlCount,
    int RegulationCount,
    int RiskCount,
    int TemplateCount,
    int ActiveIntegrationCount,
    int TotalIntegrationCount,
    int WebhookSubscriptionCount,
    string Version,
    string Environment,
    long UptimeSeconds);

public interface ISystemMetricsProvider
{
    Task<SystemMetrics> GetAsync(CancellationToken cancellationToken = default);
}
