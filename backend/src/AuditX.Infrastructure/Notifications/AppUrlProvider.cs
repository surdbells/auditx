using AuditX.Application.Abstractions.Notifications;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Notifications;

/// <summary>Supplies the web app base URL from <see cref="NotificationOptions.WebBaseUrl"/> (trailing slash trimmed).</summary>
public sealed class AppUrlProvider(IOptions<NotificationOptions> options) : IAppUrlProvider
{
    public string WebBaseUrl { get; } = (options.Value.WebBaseUrl ?? string.Empty).TrimEnd('/');
}
