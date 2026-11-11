using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Recurring sweep that deactivates time-boxed ITANDT support logins once their access window has passed. Sign-in is
/// already refused past the window and the session token is capped to it, so this is the hygiene step that flips the
/// expired accounts to Deactivated (and clears them from the active set). Idempotent — only touches not-yet-deactivated
/// support accounts whose <c>SupportAccessExpiresAt</c> is in the past.
/// </summary>
public sealed class SupportAccessExpiryJob(
    IUserRepository users,
    IClock clock,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork,
    ILogger<SupportAccessExpiryJob> logger)
{
    public const string RecurringJobId = "support-access-expiry";

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expired = await users.GetExpiredSupportAccountsAsync(now, cancellationToken);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var account in expired)
        {
            account.Deactivate(null);
        }

        audit.RecordAs(ActorType.System, RecurringJobId, null,
            AuditEventTypes.SupportChannelExpired, AuditTargetTypes.SupportChannel, null,
            payload: new { deactivated = expired.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deactivated {Count} expired ITANDT support login(s).", expired.Count);
    }
}
