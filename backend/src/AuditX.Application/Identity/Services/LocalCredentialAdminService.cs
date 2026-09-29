using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Identity.Passwords;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Identity.Services;

/// <summary>
/// Shared admin-side logic for establishing a user's local password by one of the three methods
/// (admin-set, system-generated temporary, or emailed invite). Returns the generated temporary password
/// (once) for the generate method; null otherwise.
/// </summary>
public sealed class LocalCredentialAdminService(
    PasswordService passwordService,
    IPasswordResetTokenRepository tokens,
    IEmailSender emailSender,
    IAppUrlProvider appUrls,
    IClock clock,
    ILogger<LocalCredentialAdminService> logger)
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    public async Task<string?> ApplyInitialAsync(User user, InitialPasswordMethod method, string? password, InstitutionSettings settings, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case InitialPasswordMethod.SetPassword:
                if (string.IsNullOrEmpty(password))
                {
                    throw new ValidationException([new ValidationFailure("password", "A password is required when setting it directly.")]);
                }

                await passwordService.ValidateAsync(user.Id, password, settings, cancellationToken);
                await passwordService.ApplyAsync(user.Id, password, mustChange: true, settings, cancellationToken);
                return null;

            case InitialPasswordMethod.GenerateTemp:
                var temp = TempPasswordGenerator.Generate(settings);
                // The generator guarantees policy compliance; skip reuse checks for a brand-new temporary secret.
                await passwordService.ApplyAsync(user.Id, temp, mustChange: true, settings, cancellationToken);
                return temp;

            case InitialPasswordMethod.Invite:
                await IssueInviteAsync(user, cancellationToken);
                return null;

            default:
                throw new ValidationException([new ValidationFailure("method", "Unknown initial-password method.")]);
        }
    }

    private async Task IssueInviteAsync(User user, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        foreach (var outstanding in await tokens.GetUnconsumedForUserAsync(user.Id, cancellationToken))
        {
            outstanding.Consume(now);
        }

        var raw = CredentialTokens.GenerateRawToken();
        tokens.Add(PasswordResetToken.Issue(user.Id, CredentialTokens.Hash(raw), CredentialTokenPurpose.Invite, now.Add(InviteLifetime)));
        await SendInviteEmailAsync(user, raw, cancellationToken);
    }

    private async Task SendInviteEmailAsync(User user, string rawToken, CancellationToken cancellationToken)
    {
        var baseUrl = appUrls.WebBaseUrl?.TrimEnd('/') ?? string.Empty;
        var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var body =
            $"Hello {user.FirstName},\n\n" +
            "An AuditX account has been created for you. Use the link below within the next 7 days to set your password:\n\n" +
            $"{link}\n\n" +
            $"Your username is: {user.Username}\n";

        try
        {
            var result = await emailSender.SendAsync(user.Email, "Set up your AuditX account", body, cancellationToken);
            if (!result.Success)
            {
                logger.LogWarning("Invite email to user {UserId} was not sent: {Error}", user.Id, result.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send invite email to user {UserId}", user.Id);
        }
    }
}
