using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Ac;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Ac;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using FluentValidation;

namespace AuditX.Application.Ac.Commands;

// ---- Generate a new AC pack version (GenerateACPack + CIA, in-handler) ----

/// <summary>
/// Requests asynchronous generation of a new AC pack version (M13). Returns 202 with the pack id (the pack id IS
/// the job handle). The bank-wide version is max+1; the assembled content snapshot is computed by the async worker.
/// Requires both <c>GenerateACPack</c> AND <c>CIA</c> (the dual gate is enforced in-handler; the attribute checks
/// the first globally).
/// </summary>
public sealed record GenerateAcPackCommand(
    DateOnly PeriodStart, DateOnly PeriodEnd, string? AcMeetingLabel, bool Docx) : ICommand<AcPackGenerationAcceptedDto>;

public sealed class GenerateAcPackCommandValidator : AbstractValidator<GenerateAcPackCommand>
{
    public GenerateAcPackCommandValidator()
    {
        RuleFor(x => x.PeriodStart).NotEmpty();
        RuleFor(x => x.PeriodEnd).NotEmpty();
        RuleFor(x => x).Must(x => x.PeriodEnd >= x.PeriodStart)
            .WithMessage("The reporting period end cannot be before its start.")
            .WithErrorCode("ac_pack.invalid_period");
        RuleFor(x => x.AcMeetingLabel).MaximumLength(200);
    }
}

public sealed class GenerateAcPackCommandHandler(
    IAcPackRepository packs, IAcPackGenerationQueue queue, IPermissionResolver permissions,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<GenerateAcPackCommand, AcPackGenerationAcceptedDto>
{
    public async Task<AcPackGenerationAcceptedDto> Handle(GenerateAcPackCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();

        // Dual gate: GenerateACPack (attribute) AND CIA (in-handler).
        if (!await permissions.HasPermissionAsync(actorId, PermissionKeys.Cia, cancellationToken: cancellationToken))
        {
            throw new ForbiddenAccessException("Generating an AC pack requires the Chief Internal Auditor permission.");
        }

        var nextVersion = await packs.GetMaxVersionNumberAsync(cancellationToken) + 1;
        var requestedFormats = command.Docx ? new[] { "html", "docx" } : ["html"];
        var pack = AcPack.Start(nextVersion, command.PeriodStart, command.PeriodEnd, command.AcMeetingLabel, requestedFormats, actorId, clock.UtcNow);
        packs.Add(pack);

        audit.Record(AuditEventTypes.AcPackGenerationRequested, AuditTargetTypes.AcPack, pack.Id,
            after: new { pack.VersionNumber, status = AcPackStatus.Generated.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Fire-and-forget the generation job AFTER the row is committed so the worker can load it.
        queue.Enqueue(pack.Id);

        return new AcPackGenerationAcceptedDto(pack.Id, AcPackStatus.Generated.ToString().ToLowerInvariant());
    }
}

// ---- Update the CIA supplementary text (CIA; PendingReview only) ----

public sealed record UpdateAcPackCiaTextCommand(Guid AcPackId, string? SupplementaryText) : ICommand<AcPackDto>;

public sealed class UpdateAcPackCiaTextCommandValidator : AbstractValidator<UpdateAcPackCiaTextCommand>
{
    public UpdateAcPackCiaTextCommandValidator()
    {
        RuleFor(x => x.AcPackId).NotEmpty();
        RuleFor(x => x.SupplementaryText).MaximumLength(20000);
    }
}

public sealed class UpdateAcPackCiaTextCommandHandler(
    IAcPackRepository packs, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateAcPackCiaTextCommand, AcPackDto>
{
    public async Task<AcPackDto> Handle(UpdateAcPackCiaTextCommand command, CancellationToken cancellationToken)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(command.AcPackId, cancellationToken) ?? throw new NotFoundException("AC pack", command.AcPackId);

        pack.AddSupplementaryText(command.SupplementaryText);
        audit.Record(AuditEventTypes.AcPackSupplementaryTextUpdated, AuditTargetTypes.AcPack, pack.Id,
            after: new { pack.VersionNumber, hasText = !string.IsNullOrWhiteSpace(command.SupplementaryText) });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Mapping.AcMappings.ToDto(pack);
    }
}

// ---- Approve an AC pack (CIA; single-actor, NOT maker-checker) ----

public sealed record ApproveAcPackCommand(Guid AcPackId, string? SupplementaryText) : ICommand<AcPackDto>;

public sealed class ApproveAcPackCommandValidator : AbstractValidator<ApproveAcPackCommand>
{
    public ApproveAcPackCommandValidator()
    {
        RuleFor(x => x.AcPackId).NotEmpty();
        RuleFor(x => x.SupplementaryText).MaximumLength(20000);
    }
}

public sealed class ApproveAcPackCommandHandler(
    IAcPackRepository packs, IAcPackRenderer renderer, IFileStorage storage,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveAcPackCommand, AcPackDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AcPackDto> Handle(ApproveAcPackCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(command.AcPackId, cancellationToken) ?? throw new NotFoundException("AC pack", command.AcPackId);

        // Optional last-mile supplementary text edit while still PendingReview (before the approve transition).
        if (command.SupplementaryText is not null)
        {
            pack.AddSupplementaryText(command.SupplementaryText);
        }

        // Re-render + re-seal the canonical artefacts NOW (still PendingReview) from the immutable snapshot + the FINAL
        // narrative, so the distributed/downloaded document and its SHA-256 actually contain the CIA supplementary text
        // (the generation-time render predates it). The content snapshot itself is never re-queried or mutated.
        await ResealAsync(pack, cancellationToken);

        pack.Approve(actorId, clock.UtcNow);
        audit.Record(AuditEventTypes.AcPackApproved, AuditTargetTypes.AcPack, pack.Id, after: new { pack.VersionNumber });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AcMappings.ToDto(pack);
    }

    private async Task ResealAsync(AcPack pack, CancellationToken cancellationToken)
    {
        var composition = AcMappings.ParseComposition(pack.ContentSnapshotJson)
            ?? throw new DomainException("ac_pack.not_completed", "The AC pack has no content snapshot to seal.");
        var context = new AcPackRenderContext(composition, pack.CiaSupplementaryText);

        var produced = new List<ProducedArtefact>();
        string? canonicalHash = null;
        string? canonicalKey = null;
        foreach (var format in AcMappings.ParseFormats(pack.RequestedFormatsJson))
        {
            if (!renderer.CanRender(format))
            {
                continue;
            }

            var artefact = renderer.Render(format, context);
            var ext = format == "docx" ? "docx" : "html";
            var key = $"ac-packs/{pack.Id}/v{pack.VersionNumber}/approved/{format}/{Guid.NewGuid():N}.{ext}";
            await storage.SaveAsync(key, artefact.Content, cancellationToken);
            produced.Add(new ProducedArtefact(format, key, artefact.ContentType, artefact.Content.LongLength, artefact.Sha256Hash));
            if (format == "html")
            {
                canonicalHash = artefact.Sha256Hash;
                canonicalKey = key;
            }
        }

        var first = produced.Count > 0 ? produced[0] : throw new DomainException("ac_pack.no_artefacts", "The AC pack produced no artefacts to seal.");
        pack.ResealArtefacts(canonicalHash ?? first.Sha256, canonicalKey ?? first.FileKey, JsonSerializer.Serialize(produced, JsonOptions), produced.Count);
    }
}

// ---- Distribute an approved AC pack to the AC cohort (CIA) ----

public sealed record DistributeAcPackCommand(Guid AcPackId) : ICommand<AcPackDistributionResultDto>;

public sealed class DistributeAcPackCommandValidator : AbstractValidator<DistributeAcPackCommand>
{
    public DistributeAcPackCommandValidator() => RuleFor(x => x.AcPackId).NotEmpty();
}

public sealed class DistributeAcPackCommandHandler(
    IAcPackRepository packs, IUserRepository users, ICurrentUser currentUser,
    IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DistributeAcPackCommand, AcPackDistributionResultDto>
{
    public async Task<AcPackDistributionResultDto> Handle(DistributeAcPackCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var pack = await packs.GetByIdAsync(command.AcPackId, cancellationToken) ?? throw new NotFoundException("AC pack", command.AcPackId);

        if (pack.Status is not (AcPackStatus.Approved or AcPackStatus.Distributed))
        {
            throw new DomainException("ac_pack.not_distributable", "Only an approved AC pack can be distributed.");
        }

        // Resolve the AC cohort: members + chair (deduped). The deferred external-NED token access is not in scope,
        // so recipients are directory users holding the seeded AC roles.
        var members = await users.GetActiveByRoleNameAsync(AcRoles.AuditCommitteeMember, cancellationToken);
        var chairs = await users.GetActiveByRoleNameAsync(AcRoles.AuditCommitteeChair, cancellationToken);
        var cohort = members.Concat(chairs).Select(u => u.Id).Distinct().ToArray();
        var alreadySent = pack.Distributions.Select(d => d.RecipientUserId).ToHashSet();
        var recipientIds = cohort.Where(id => !alreadySent.Contains(id)).ToArray();

        if (recipientIds.Length == 0)
        {
            // No cohort at all → genuine error. Cohort exists but everyone already has it → idempotent no-op (re-distribute).
            if (cohort.Length == 0)
            {
                throw new DomainException("ac_pack.no_recipients", "No audit-committee recipients are available to distribute to.");
            }

            return new AcPackDistributionResultDto(0);
        }

        foreach (var recipientId in recipientIds)
        {
            pack.RecordDistribution(recipientId, actorId, clock.UtcNow);
        }

        audit.Record(AuditEventTypes.AcPackDistributed, AuditTargetTypes.AcPack, pack.Id,
            after: new { pack.VersionNumber, recipientCount = recipientIds.Length });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcPackDistributionResultDto(recipientIds.Length);
    }
}
