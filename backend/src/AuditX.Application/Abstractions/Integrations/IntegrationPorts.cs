namespace AuditX.Application.Abstractions.Integrations;

/// <summary>Encrypts/decrypts integration credentials at rest (ASP.NET Core DataProtection / HSM in production).</summary>
public interface ICredentialProtector
{
    byte[] Protect(string plaintext);

    string Unprotect(byte[] ciphertext);
}

/// <summary>Outcome of an outbound HTTP webhook send.</summary>
public sealed record WebhookSendResult(bool Success, string? Error);

/// <summary>Sends a signed webhook payload to a destination URL (HMAC-SHA-256 in the signature header).</summary>
public interface IWebhookSender
{
    Task<WebhookSendResult> SendAsync(string destinationUrl, string payloadJson, string signature, int timeoutSeconds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Decides whether a URL targets the bank's internal network. Webhook destinations and runtime egress
/// must be internal only (US-M14-015).
/// </summary>
public interface IInternalNetworkPolicy
{
    bool IsInternal(string url);
}

/// <summary>Result of a benign integration connectivity test (US-M14-005).</summary>
public sealed record IntegrationTestResult(bool Success, string Detail);

/// <summary>Performs a connectivity check appropriate to an integration type.</summary>
public interface IIntegrationTester
{
    Task<IntegrationTestResult> TestAsync(Domain.Integrations.IntegrationConfiguration integration, string? decryptedCredentials, CancellationToken cancellationToken = default);
}

/// <summary>Exports an audit-trail/security event to the bank's SIEM (syslog or HTTPS). Failures must not block callers.</summary>
public interface ISiemExporter
{
    Task ExportAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
}
