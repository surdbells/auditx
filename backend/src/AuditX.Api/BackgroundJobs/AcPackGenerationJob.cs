using AuditX.Application.Abstractions.Ac;
using AuditX.Application.Ac.Generation;
using Hangfire;

namespace AuditX.Api.BackgroundJobs;

/// <summary>
/// Fire-and-forget Hangfire job that runs an AC pack's generation off the request thread (M13). Enqueued from the
/// generate handler via <see cref="IAcPackGenerationQueue"/>; resolves the application <see cref="AcPackGenerationService"/>.
/// The service is idempotent on the pack id, so a Hangfire requeue never produces a duplicate version.
/// </summary>
public sealed class AcPackGenerationJob(AcPackGenerationService service)
{
    public Task RunAsync(Guid acPackId, CancellationToken cancellationToken)
        => service.RunAsync(acPackId, cancellationToken);
}

/// <summary>Enqueues <see cref="AcPackGenerationJob"/> as a fire-and-forget Hangfire job (the pack id IS the handle).</summary>
public sealed class HangfireAcPackGenerationQueue(IBackgroundJobClient backgroundJobs) : IAcPackGenerationQueue
{
    public void Enqueue(Guid acPackId)
        => backgroundJobs.Enqueue<AcPackGenerationJob>(job => job.RunAsync(acPackId, CancellationToken.None));
}
