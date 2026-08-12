using System.Net.Http.Json;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Notifications;

/// <summary>
/// Production Microsoft Teams sender: POSTs a MessageCard to a channel's incoming webhook. The webhook URL is
/// resolved from configuration by channel reference (only the default channel today) and never persisted, so a
/// dispatch stores the stable reference, not the secret URL. Maps HTTP outcomes to the retry classification.
/// </summary>
public sealed class TeamsWebhookSender(IHttpClientFactory httpClientFactory, IOptions<NotificationOptions> options, ILogger<TeamsWebhookSender> logger) : ITeamsSender
{
    private readonly TeamsOptions _teams = options.Value.Teams;

    private string? ResolveUrl(string? channelRef)
    {
        // Only the default channel is wired today; an unknown reference resolves to nothing (skipped/failed).
        if (string.IsNullOrWhiteSpace(channelRef) || string.Equals(channelRef, ITeamsSender.DefaultChannelRef, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(_teams.DefaultWebhookUrl) ? null : _teams.DefaultWebhookUrl;
        }

        return null;
    }

    public bool IsConfigured(string? channelRef = null) => ResolveUrl(channelRef) is not null;

    public async Task<ChannelSendResult> SendAsync(string channelRef, string? title, string body, CancellationToken cancellationToken = default)
    {
        var url = ResolveUrl(channelRef);
        if (url is null)
        {
            return ChannelSendResult.Permanent($"No Teams webhook configured for '{channelRef}'.");
        }

        // Legacy MessageCard — the format Teams incoming webhooks accept without an app manifest.
        var card = new Dictionary<string, object?>
        {
            ["@type"] = "MessageCard",
            ["@context"] = "http://schema.org/extensions",
            ["summary"] = string.IsNullOrWhiteSpace(title) ? "AuditX notification" : title,
            ["themeColor"] = "4F46E5",
            ["title"] = title,
            ["text"] = body,
        };

        try
        {
            using var client = httpClientFactory.CreateClient("teams");
            var response = await client.PostAsJsonAsync(url, card, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ChannelSendResult.Sent(providerResponse: $"{(int)response.StatusCode} {response.ReasonPhrase}");
            }

            // 4xx (bar 429 throttling) is a permanent problem with the payload/URL; 5xx and 429 are transient.
            var permanent = (int)response.StatusCode is >= 400 and < 500 && (int)response.StatusCode != 429;
            var error = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            return permanent ? ChannelSendResult.Permanent(error) : ChannelSendResult.Transient(error);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Transient Teams webhook failure for {ChannelRef}", channelRef);
            return ChannelSendResult.Transient(ex.Message);
        }
    }
}

/// <summary>Development Teams sender: logs the card and reports success (no real webhook needed locally / in tests).</summary>
public sealed class LoggingTeamsSender(ILogger<LoggingTeamsSender> logger) : ITeamsSender
{
    // Always "configured" in development so the pipeline exercises the Teams channel end-to-end against the sink.
    public bool IsConfigured(string? channelRef = null) => true;

    public Task<ChannelSendResult> SendAsync(string channelRef, string? title, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV TEAMS] channel={ChannelRef} title={Title}", channelRef, title);
        return Task.FromResult(ChannelSendResult.Sent(providerMessageId: $"dev-{Guid.CreateVersion7():N}"));
    }
}
