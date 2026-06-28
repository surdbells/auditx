using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Ac;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Ac.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace AuditX.Application.Ac.Generation;

/// <summary>
/// Runs an AC pack's generation off the request thread (M13). Invoked by the Hangfire <c>AcPackGenerationJob</c>.
/// IDEMPOTENT on the pack id (mirrors <c>ReportGenerationService</c>): a re-run (double-submit, Hangfire requeue)
/// is a no-op unless the pack is still <c>generated</c>, so generation never produces a duplicate version. On
/// success it assembles the M9-analytics composition, SNAPSHOTS it onto the pack, renders each requested format,
/// stores the artefacts via <see cref="IFileStorage"/>, hashes the canonical HTML, completes the pack
/// (post-commit → <c>ac_pack_generated</c> → M10 to CIA + trail). Any failure marks the pack <c>failed</c>.
/// </summary>
public sealed class AcPackGenerationService(
    IAcPackRepository packs,
    AcPackContentAssembler assembler,
    IAcPackRenderer renderer,
    IFileStorage storage,
    IClock clock,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    ILogger<AcPackGenerationService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(Guid acPackId, CancellationToken cancellationToken)
    {
        var pack = await packs.GetByIdAsync(acPackId, cancellationToken);
        if (pack is null)
        {
            logger.LogWarning("AC pack {AcPackId} not found; skipping generation.", acPackId);
            return;
        }

        // Idempotency: only a still-generated pack is processed. A requeue/double-run sees running/pending/failed
        // and exits, so no duplicate version is ever produced.
        if (pack.Status != AcPackStatus.Generated)
        {
            logger.LogInformation("AC pack {AcPackId} is {Status}; generation already handled.", acPackId, pack.Status);
            return;
        }

        try
        {
            pack.MarkRunning();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var composition = await assembler.AssembleAsync(pack, cancellationToken);
            var snapshotJson = JsonSerializer.Serialize(composition, JsonOptions);
            var context = new AcPackRenderContext(composition, pack.CiaSupplementaryText);

            var requestedFormats = AcMappings.ParseFormats(pack.RequestedFormatsJson);
            var produced = new List<ProducedArtefact>();
            string? canonicalHash = null;
            string? canonicalKey = null;

            foreach (var format in requestedFormats)
            {
                if (!renderer.CanRender(format))
                {
                    logger.LogWarning("AC pack {AcPackId}: format '{Format}' is not supported by the active renderer; skipping.", acPackId, format);
                    continue;
                }

                var artefact = renderer.Render(format, context);
                var ext = format == "docx" ? "docx" : "html";
                var key = $"ac-packs/{pack.Id}/v{pack.VersionNumber}/{format}/{Guid.NewGuid():N}.{ext}";
                await storage.SaveAsync(key, artefact.Content, cancellationToken);
                produced.Add(new ProducedArtefact(format, key, artefact.ContentType, artefact.Content.LongLength, artefact.Sha256Hash));

                if (format == "html")
                {
                    canonicalHash = artefact.Sha256Hash;
                    canonicalKey = key;
                }
            }

            // The canonical, hashed artefact is always the HTML one (it is always produced).
            var firstProduced = produced.Count > 0 ? produced[0] : throw new InvalidOperationException($"AC pack {acPackId} produced no artefacts.");
            canonicalHash ??= firstProduced.Sha256;
            canonicalKey ??= firstProduced.FileKey;

            var producedJson = JsonSerializer.Serialize(produced, JsonOptions);
            pack.Complete(snapshotJson, canonicalHash, canonicalKey, producedJson, produced.Count, clock.UtcNow);
            auditRecorder.RecordAs(ActorType.System, "ac", pack.GeneratedBy,
                AuditEventTypes.AcPackGenerated, AuditTargetTypes.AcPack, pack.Id,
                after: new { pack.VersionNumber, sha256 = canonicalHash, formats = produced.Count });
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("AC pack {AcPackId} (v{Version}) generated with {Count} artefact(s).", acPackId, pack.VersionNumber, produced.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AC pack {AcPackId} generation failed.", acPackId);
            await FailPackAsync(acPackId, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task FailPackAsync(Guid acPackId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var pack = await packs.GetByIdAsync(acPackId, cancellationToken);
            if (pack is null || pack.Status is AcPackStatus.PendingReview or AcPackStatus.Approved or AcPackStatus.Distributed or AcPackStatus.Failed)
            {
                return;
            }

            pack.Fail(Truncate(reason, 1000));
            auditRecorder.RecordAs(ActorType.System, "ac", pack.GeneratedBy,
                AuditEventTypes.AcPackGenerationFailed, AuditTargetTypes.AcPack, pack.Id,
                after: new { pack.VersionNumber, reason = Truncate(reason, 1000) });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark AC pack {AcPackId} as failed.", acPackId);
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? "Generation failed." : value.Length <= max ? value : value[..max];
}
