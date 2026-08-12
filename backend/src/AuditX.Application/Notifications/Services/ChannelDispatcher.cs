using AuditX.Application.Abstractions.Notifications;
using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;

namespace AuditX.Application.Notifications.Services;

/// <summary>
/// Routes a dispatch to the right channel sender. Centralised so the initial send, the automatic retry sweep,
/// and the manual retry command all treat channels identically (add a channel here, not in three places).
/// </summary>
internal static class ChannelDispatcher
{
    public static Task<ChannelSendResult> SendAsync(
        NotificationDispatch dispatch, IEmailSender email, ISmsSender sms, ITeamsSender teams, CancellationToken cancellationToken)
        => dispatch.Channel switch
        {
            NotificationChannel.Email => email.SendAsync(dispatch.RecipientAddress, dispatch.RenderedSubject, dispatch.RenderedBody, cancellationToken),
            NotificationChannel.Sms => sms.SendAsync(dispatch.RecipientAddress, dispatch.RenderedBody, cancellationToken),
            NotificationChannel.Teams => teams.SendAsync(dispatch.RecipientAddress, dispatch.RenderedSubject, dispatch.RenderedBody, cancellationToken),
            _ => Task.FromResult(ChannelSendResult.Permanent($"Unsupported channel '{dispatch.Channel}'.")),
        };
}
