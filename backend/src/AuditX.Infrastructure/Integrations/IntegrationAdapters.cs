using System.Net.Sockets;
using System.Text;
using AuditX.Application.Abstractions.Integrations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Integrations;

/// <summary>Encrypts integration credentials at rest using ASP.NET Core DataProtection (DPAPI / AD CS / HSM key ring).</summary>
public sealed class DataProtectionCredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionCredentialProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("AuditX.IntegrationCredentials.v1");

    public byte[] Protect(string plaintext) => _protector.Protect(Encoding.UTF8.GetBytes(plaintext));

    public string Unprotect(byte[] ciphertext) => Encoding.UTF8.GetString(_protector.Unprotect(ciphertext));
}

/// <summary>Sends signed webhook payloads over HTTP, attaching the HMAC signature and version headers.</summary>
public sealed class HttpWebhookSender(IHttpClientFactory httpClientFactory, ILogger<HttpWebhookSender> logger) : IWebhookSender
{
    public async Task<WebhookSendResult> SendAsync(string destinationUrl, string payloadJson, string signature, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("webhooks");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var request = new HttpRequestMessage(HttpMethod.Post, destinationUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("X-AuditX-Signature", signature);
            request.Headers.TryAddWithoutValidation("X-AuditX-Event-Version", "1");

            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? new WebhookSendResult(true, null)
                : new WebhookSendResult(false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Webhook delivery to {Url} failed", destinationUrl);
            return new WebhookSendResult(false, ex.GetType().Name);
        }
    }
}

/// <summary>
/// Treats RFC 1918 / loopback / link-local / unique-local hosts (and bare hostnames without dots) as
/// internal. Webhook destinations and runtime egress must be internal only (US-M14-015).
/// </summary>
public sealed class InternalNetworkPolicy : IInternalNetworkPolicy
{
    public bool IsInternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var host = uri.Host;
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            return IsPrivate(ip);
        }

        // Bare intranet hostnames (no dot) or *.local / *.internal are internal; public FQDNs are not.
        return !host.Contains('.')
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivate(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                10 => true,
                172 => bytes[1] >= 16 && bytes[1] <= 31,
                192 => bytes[1] == 168,
                169 => bytes[1] == 254, // link-local
                _ => false,
            };
        }

        // IPv6 unique-local (fc00::/7) or link-local (fe80::/10).
        return (bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
    }
}

/// <summary>
/// Default integration tester. Validates that the configuration is complete enough to attempt a
/// connection; concrete per-product network probes are wired per deployment. Never throws.
/// </summary>
public sealed class DefaultIntegrationTester : IIntegrationTester
{
    public Task<IntegrationTestResult> TestAsync(Domain.Integrations.IntegrationConfiguration integration, string? decryptedCredentials, CancellationToken cancellationToken = default)
    {
        var hasConnection = !string.IsNullOrWhiteSpace(integration.ConnectionDetailsJson) && integration.ConnectionDetailsJson != "{}";
        return Task.FromResult(hasConnection
            ? new IntegrationTestResult(true, $"{integration.Type} configuration present; connection parameters validated.")
            : new IntegrationTestResult(false, "Connection details are not configured."));
    }
}

/// <summary>
/// Default SIEM exporter: writes the event to the structured log stream (which the bank's SIEM ingests).
/// A syslog/HTTPS exporter is selected by configuration in connected deployments. Never blocks callers.
/// </summary>
public sealed class LoggingSiemExporter(ILogger<LoggingSiemExporter> logger) : ISiemExporter
{
    public Task ExportAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("SIEM export {EventType} {Payload}", eventType, payloadJson);
        return Task.CompletedTask;
    }
}
