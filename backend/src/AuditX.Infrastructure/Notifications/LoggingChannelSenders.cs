using AuditX.Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Notifications;

/// <summary>Development email sender: logs the message and reports success (no real SMTP needed locally / in tests).</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<ChannelSendResult> SendAsync(string toAddress, string? subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV EMAIL] to={To} subject={Subject}", toAddress, subject);
        return Task.FromResult(ChannelSendResult.Sent(providerMessageId: $"dev-{Guid.CreateVersion7():N}"));
    }
}

/// <summary>Development SMS sender: logs the message and reports success.</summary>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task<ChannelSendResult> SendAsync(string toNumber, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV SMS] to={To}", toNumber);
        return Task.FromResult(ChannelSendResult.Sent(providerMessageId: $"dev-{Guid.CreateVersion7():N}"));
    }
}
