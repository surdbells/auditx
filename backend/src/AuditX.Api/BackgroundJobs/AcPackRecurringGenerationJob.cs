using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Ac;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.Ac;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Recurring Hangfire job (M13) that auto-generates a quarterly AC pack. Creates a new pack version for the just-ended
/// quarter (System actor), then enqueues the same idempotent <see cref="AcPackGenerationJob"/> the on-demand path uses.
/// <c>[DisableConcurrentExecution]</c> singleton so a slow run never overlaps the next quarterly trigger (which would
/// race the unique version index). The schedule defaults to quarterly via the registration in Program.cs.
/// </summary>
public sealed class AcPackRecurringGenerationJob(
    IAcPackRepository packs,
    IAcPackGenerationQueue queue,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<AcPackRecurringGenerationJob> logger)
{
    public const string RecurringJobId = "ac-pack-quarterly-generation";

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var nowUtc = clock.UtcNow;
        var (periodStart, periodEnd, label) = PreviousQuarter(nowUtc);

        var nextVersion = await packs.GetMaxVersionNumberAsync(cancellationToken) + 1;
        var pack = AcPack.Start(nextVersion, periodStart, periodEnd, label, ["html", "docx"], actorId: Guid.Empty, nowUtc);
        packs.Add(pack);

        audit.RecordAs(ActorType.System, RecurringJobId, null,
            AuditEventTypes.AcPackGenerationRequested, AuditTargetTypes.AcPack, pack.Id,
            after: new { pack.VersionNumber, period = label, status = AcPackStatus.Generated.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        queue.Enqueue(pack.Id);
        logger.LogInformation("Quarterly AC pack v{Version} ({Period}) queued for generation.", pack.VersionNumber, label);
    }

    /// <summary>The calendar quarter immediately before <paramref name="nowUtc"/> (start/end dates + a human label).</summary>
    private static (DateOnly Start, DateOnly End, string Label) PreviousQuarter(DateTimeOffset nowUtc)
    {
        var currentQuarter = (nowUtc.Month - 1) / 3; // 0..3
        var year = nowUtc.Year;
        var prevQuarter = currentQuarter - 1;
        if (prevQuarter < 0)
        {
            prevQuarter = 3;
            year -= 1;
        }

        var startMonth = prevQuarter * 3 + 1;
        var start = new DateOnly(year, startMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);
        return (start, end, $"Q{prevQuarter + 1} {year}");
    }
}
