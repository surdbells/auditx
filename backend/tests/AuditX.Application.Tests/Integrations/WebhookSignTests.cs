using System.Security.Cryptography;
using System.Text;
using AuditX.Application.Integrations.Webhooks;

namespace AuditX.Application.Tests.Integrations;

public sealed class WebhookSignTests
{
    [Fact]
    public void Sign_matches_independent_hmac_sha256()
    {
        const string payload = "{\"event\":\"exception_raised\"}";
        const string secret = "super-secret-value-1234567890";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        Assert.Equal(expected, WebhookDispatchService.Sign(payload, secret));
    }

    [Fact]
    public void Sign_is_deterministic_and_secret_sensitive()
    {
        Assert.Equal(WebhookDispatchService.Sign("p", "s1"), WebhookDispatchService.Sign("p", "s1"));
        Assert.NotEqual(WebhookDispatchService.Sign("p", "s1"), WebhookDispatchService.Sign("p", "s2"));
    }
}
