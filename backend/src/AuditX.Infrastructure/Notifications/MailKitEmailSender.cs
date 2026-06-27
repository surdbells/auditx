using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditX.Infrastructure.Notifications;

/// <summary>
/// Production email sender over the bank's on-prem SMTP relay using the built-in <see cref="SmtpClient"/>
/// (no external NuGet). A vetted MailKit adapter can replace this once a non-vulnerable version is pinned.
/// </summary>
public sealed class SmtpEmailSender(IOptions<NotificationOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _smtp = options.Value.Smtp;

    public async Task<ChannelSendResult> SendAsync(string toAddress, string? subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host))
        {
            return ChannelSendResult.Permanent("SMTP host is not configured.");
        }

        try
        {
#pragma warning disable SYSLIB0014 // SmtpClient is obsolete; acceptable for on-prem relay until a vetted replacement is pinned.
            using var client = new SmtpClient(_smtp.Host, _smtp.Port) { EnableSsl = _smtp.UseStartTls };
#pragma warning restore SYSLIB0014
            if (!string.IsNullOrWhiteSpace(_smtp.Username))
            {
                client.Credentials = new NetworkCredential(_smtp.Username, _smtp.Password ?? string.Empty);
            }

            using var message = new MailMessage(new MailAddress(_smtp.FromAddress, _smtp.FromName), new MailAddress(toAddress))
            {
                Subject = subject ?? string.Empty,
                Body = body,
            };

            await client.SendMailAsync(message, cancellationToken);
            return ChannelSendResult.Sent();
        }
        catch (SmtpFailedRecipientException ex)
        {
            logger.LogWarning(ex, "Permanent SMTP recipient failure sending to {To}", toAddress);
            return ChannelSendResult.Permanent(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Transient SMTP failure sending to {To}", toAddress);
            return ChannelSendResult.Transient(ex.Message);
        }
    }
}

/// <summary>Production SMS sender over the bank gateway HTTP API. Permanent-fails until a gateway URL is configured.</summary>
public sealed class HttpSmsSender(IHttpClientFactory httpClientFactory, IOptions<NotificationOptions> options, ILogger<HttpSmsSender> logger) : ISmsSender
{
    private readonly SmsOptions _sms = options.Value.Sms;

    public async Task<ChannelSendResult> SendAsync(string toNumber, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_sms.GatewayUrl))
        {
            return ChannelSendResult.Permanent("SMS gateway is not configured.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient("sms");
            var response = await client.PostAsJsonAsync(_sms.GatewayUrl, new { to = toNumber, message = body }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ChannelSendResult.Sent(providerResponse: (int)response.StatusCode + " " + response.ReasonPhrase);
            }

            var permanent = (int)response.StatusCode is >= 400 and < 500 && (int)response.StatusCode != 429;
            var error = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            return permanent ? ChannelSendResult.Permanent(error) : ChannelSendResult.Transient(error);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Transient SMS failure sending to {To}", toNumber);
            return ChannelSendResult.Transient(ex.Message);
        }
    }
}
