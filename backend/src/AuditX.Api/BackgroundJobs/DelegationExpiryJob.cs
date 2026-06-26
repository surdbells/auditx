using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Delegations;

namespace AuditX.Api.BackgroundJobs;

/// <summary>Hourly job that expires delegations whose window has elapsed (US-M1-028).</summary>
public sealed class DelegationExpiryJob(IDispatcher dispatcher, ILogger<DelegationExpiryJob> logger)
{
    public const string RecurringJobId = "delegation-expiry";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var expired = await dispatcher.Send(new ExpireDelegationsCommand(), cancellationToken);
        if (expired > 0)
        {
            logger.LogInformation("Expired {Count} delegation(s).", expired);
        }
    }
}
