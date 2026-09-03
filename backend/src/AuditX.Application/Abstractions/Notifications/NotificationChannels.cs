namespace AuditX.Application.Abstractions.Notifications;

/// <summary>The serialized fact handed to the notification pipeline for one raised domain event (M10).</summary>
public sealed record DomainEventEnvelope(string EventType, Guid EventId, DateTimeOffset OccurredAtUtc, Guid? ActorUserId, string PayloadJson);

/// <summary>Outcome of attempting to send over a channel; drives retry classification.</summary>
public sealed record ChannelSendResult(bool Success, string? ProviderMessageId, string? ProviderResponse, bool IsPermanentFailure, string? Error)
{
    public static ChannelSendResult Sent(string? providerMessageId = null, string? providerResponse = null)
        => new(true, providerMessageId, providerResponse, false, null);

    public static ChannelSendResult Transient(string error) => new(false, null, null, false, error);

    public static ChannelSendResult Permanent(string error) => new(false, null, null, true, error);
}

/// <summary>Sends an email (M10). On-prem SMTP in production; a logging sink in development.</summary>
public interface IEmailSender
{
    Task<ChannelSendResult> SendAsync(string toAddress, string? subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>Sends an SMS (M10) via the bank gateway; a logging sink in development.</summary>
public interface ISmsSender
{
    Task<ChannelSendResult> SendAsync(string toNumber, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Posts a notification to a Microsoft Teams channel via an incoming webhook (M10). The webhook URL is held in
/// configuration and never persisted, so the pipeline addresses a dispatch by a stable channel reference
/// (e.g. <c>"teams:default"</c>) that the sender resolves to the real URL. <see cref="IsConfigured"/> lets the
/// pipeline skip creating Teams dispatches when no webhook is set up, rather than manufacturing guaranteed failures.
/// </summary>
public interface ITeamsSender
{
    /// <summary>The stable dispatch address for the default Teams channel; also the value stored on the dispatch.</summary>
    const string DefaultChannelRef = "teams:default";

    /// <summary>Whether a webhook is configured for the given channel reference (default when null/empty).</summary>
    bool IsConfigured(string? channelRef = null);

    Task<ChannelSendResult> SendAsync(string channelRef, string? title, string body, CancellationToken cancellationToken = default);
}

/// <summary>Renders a notification template (Scriban) to a subject + body over the event model.</summary>
public interface ITemplateRenderer
{
    (string? Subject, string Body) Render(string? subjectTemplate, string bodyTemplate, IReadOnlyDictionary<string, object?> model);
}

/// <summary>
/// Supplies the web app's public base URL (e.g. <c>https://auditx.bank.local</c>) so notification templates can
/// build deep links into the SPA (exposed to every template as <c>{{ AppBaseUrl }}</c>). Empty when unconfigured,
/// in which case templates fall back to a relative path.
/// </summary>
public interface IAppUrlProvider
{
    string WebBaseUrl { get; }
}
