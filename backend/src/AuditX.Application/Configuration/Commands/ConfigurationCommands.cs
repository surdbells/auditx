using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Configuration.Dtos;
using AuditX.Application.Configuration.Mapping;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Configuration;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Configuration.Commands;

// ---- Create draft ----

public sealed record CreateConfigurationDraftCommand(string Domain, string DefinitionJson, string ChangeReason) : ICommand<ConfigurationActionResult>;

public sealed class CreateConfigurationDraftCommandValidator : AbstractValidator<CreateConfigurationDraftCommand>
{
    public CreateConfigurationDraftCommandValidator()
    {
        RuleFor(x => x.Domain).NotEmpty();
        RuleFor(x => x.DefinitionJson).NotEmpty();
        RuleFor(x => x.ChangeReason).NotEmpty().MinimumLength(20);
    }
}

public sealed class CreateConfigurationDraftCommandHandler(
    IBankConfigurationRepository configurations, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateConfigurationDraftCommand, ConfigurationActionResult>
{
    public async Task<ConfigurationActionResult> Handle(CreateConfigurationDraftCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        if (!ConfigurationDomains.IsKnown(command.Domain))
        {
            throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{command.Domain}'.");
        }

        // Per-domain JSON schema validation (G1) — bad definition → 422 before anything is persisted.
        ConfigurationDefinitions.Validate(command.Domain, command.DefinitionJson);

        var next = await configurations.GetMaxVersionNumberAsync(command.Domain, cancellationToken) + 1;
        var draft = BankConfiguration.CreateDraft(command.Domain, next, command.DefinitionJson, command.ChangeReason, actorId, clock.UtcNow);
        configurations.Add(draft);
        audit.Record(AuditEventTypes.ConfigurationVersionCreated, AuditTargetTypes.BankConfiguration, draft.Id,
            before: null, after: new { draft.Domain, draft.VersionNumber, draft.DefinitionJson, draft.ChangeReason });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConfigurationActionResult(draft.ToDto(), null);
    }
}

// ---- Activate ----

/// <summary>
/// The serialized intent captured when configuration activation is held by the maker-checker gate.
/// <paramref name="RolledBackFromVersion"/> is set only for rollbacks, so the executor (replaying on approval) can
/// record the <c>configuration_rolled_back</c> trail event on the gated path too.
/// </summary>
public sealed record ConfigActivationPayload(Guid ConfigurationId, string ChangeReason, int? RolledBackFromVersion = null);

public sealed record ActivateConfigurationVersionCommand(string Domain, int VersionNumber, string ChangeReason) : ICommand<ConfigurationActionResult>;

public sealed class ActivateConfigurationVersionCommandValidator : AbstractValidator<ActivateConfigurationVersionCommand>
{
    public ActivateConfigurationVersionCommandValidator()
    {
        RuleFor(x => x.Domain).NotEmpty();
        RuleFor(x => x.VersionNumber).GreaterThan(0);
        RuleFor(x => x.ChangeReason).NotEmpty().MinimumLength(20);
    }
}

public sealed class ActivateConfigurationVersionCommandHandler(
    IBankConfigurationRepository configurations, MakerCheckerGateService gateService, IActiveConfigurationProvider activeProvider,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateConfigurationVersionCommand, ConfigurationActionResult>
{
    public async Task<ConfigurationActionResult> Handle(ActivateConfigurationVersionCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        if (!ConfigurationDomains.IsKnown(command.Domain))
        {
            throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{command.Domain}'.");
        }

        var target = await configurations.GetByDomainVersionAsync(command.Domain, command.VersionNumber, cancellationToken)
            ?? throw new NotFoundException("Configuration version", $"{command.Domain}/v{command.VersionNumber}");
        if (target.IsActive)
        {
            throw new ConflictException("configuration.already_active", "This configuration version is already active.");
        }

        // Re-validate the stored definition at activation time (G1) — never activate a definition that no longer
        // passes the current schema.
        ConfigurationDefinitions.Validate(target.Domain, target.DefinitionJson);

        // Activation is the gated policy change (maker-checker on activate only). The executor performs the switch.
        var payload = AppJson.Serialize(new ConfigActivationPayload(target.Id, command.ChangeReason));
        var pendingId = await gateService.TryCaptureAsync(MakerCheckerActionTypes.ConfigActivation, AuditTargetTypes.BankConfiguration, target.Id, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfigurationActionResult(null, id);
        }

        // Direct (ungated) path: the set-based deactivate + tracked activate must commit atomically, else a failed
        // save could leave the domain with zero active versions. Invalidate the cache only AFTER the commit.
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await ConfigurationActivation.SwitchAsync(configurations, target, actorId, audit, clock, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Unit.Value;
        }, cancellationToken);
        activeProvider.Invalidate(target.Domain);
        return new ConfigurationActionResult(target.ToDto(), null);
    }
}

// ---- Rollback ----

public sealed record RollbackConfigurationCommand(string Domain, int ToVersionNumber, string ChangeReason) : ICommand<ConfigurationActionResult>;

public sealed class RollbackConfigurationCommandValidator : AbstractValidator<RollbackConfigurationCommand>
{
    public RollbackConfigurationCommandValidator()
    {
        RuleFor(x => x.Domain).NotEmpty();
        RuleFor(x => x.ToVersionNumber).GreaterThan(0);
        RuleFor(x => x.ChangeReason).NotEmpty().MinimumLength(20);
    }
}

public sealed class RollbackConfigurationCommandHandler(
    IBankConfigurationRepository configurations, MakerCheckerGateService gateService, IActiveConfigurationProvider activeProvider,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RollbackConfigurationCommand, ConfigurationActionResult>
{
    public async Task<ConfigurationActionResult> Handle(RollbackConfigurationCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        if (!ConfigurationDomains.IsKnown(command.Domain))
        {
            throw new DomainException("configuration.unknown_domain", $"Unknown configuration domain '{command.Domain}'.");
        }

        var source = await configurations.GetByDomainVersionAsync(command.Domain, command.ToVersionNumber, cancellationToken)
            ?? throw new NotFoundException("Configuration version", $"{command.Domain}/v{command.ToVersionNumber}");

        // Rolling back to the version that is already active is a no-op — reject rather than mint a redundant
        // forward version (and a pointless maker-checker round-trip).
        if (source.IsActive)
        {
            throw new ConflictException("configuration.already_active", "That configuration version is already active.");
        }

        // Re-validate the source definition before reviving it (G1) — never roll forward an invalid definition.
        ConfigurationDefinitions.Validate(source.Domain, source.DefinitionJson);

        // Rollback copies the chosen version's definition into a NEW forward version (the original is untouched), then
        // activates that new version via the same gated path. The change reason is retained on the new version.
        var next = await configurations.GetMaxVersionNumberAsync(command.Domain, cancellationToken) + 1;
        var newVersion = BankConfiguration.CreateDraft(command.Domain, next, source.DefinitionJson, command.ChangeReason, actorId, clock.UtcNow);
        configurations.Add(newVersion);
        audit.Record(AuditEventTypes.ConfigurationVersionCreated, AuditTargetTypes.BankConfiguration, newVersion.Id,
            before: null, after: new { newVersion.Domain, newVersion.VersionNumber, newVersion.DefinitionJson, newVersion.ChangeReason, rolledBackFrom = source.VersionNumber });

        // Carry the rollback provenance through the payload so the executor emits configuration_rolled_back on the
        // gated path too (otherwise the rollback trail is lost whenever activation is maker-checker-gated).
        var payload = AppJson.Serialize(new ConfigActivationPayload(newVersion.Id, command.ChangeReason, source.VersionNumber));
        var pendingId = await gateService.TryCaptureAsync(MakerCheckerActionTypes.ConfigActivation, AuditTargetTypes.BankConfiguration, newVersion.Id, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfigurationActionResult(null, id);
        }

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await ConfigurationActivation.SwitchAsync(configurations, newVersion, actorId, audit, clock, ct);
            audit.Record(AuditEventTypes.ConfigurationRolledBack, AuditTargetTypes.BankConfiguration, newVersion.Id,
                payload: new { newVersion.Domain, fromVersion = source.VersionNumber, newVersion = newVersion.VersionNumber });
            await unitOfWork.SaveChangesAsync(ct);
            return Unit.Value;
        }, cancellationToken);
        activeProvider.Invalidate(newVersion.Domain);
        return new ConfigurationActionResult(newVersion.ToDto(), null);
    }
}

/// <summary>Shared atomic active-configuration switch used by the direct activate/rollback path and the executor.</summary>
internal static class ConfigurationActivation
{
    /// <summary>
    /// Switch the active version for a domain to <paramref name="target"/>, attributing the trail to
    /// <paramref name="actorId"/> (the maker, when replayed by the executor). Records the prior-active definition as
    /// <c>before</c> and the new one as <c>after</c> (G4). The CALLER invalidates the active-config cache AFTER the
    /// surrounding transaction commits (invalidating inline lets a concurrent reader re-seed the stale version).
    /// </summary>
    public static async Task SwitchAsync(
        IBankConfigurationRepository configurations, BankConfiguration target,
        Guid actorId, IAuditRecorder audit, IClock clock, CancellationToken cancellationToken)
    {
        // Capture the prior-active definition for the before/after trail before we clear its flag.
        var prior = await configurations.GetActiveAsync(target.Domain, cancellationToken);
        object? before = prior is null ? null : new { prior.VersionNumber, prior.DefinitionJson };

        // Deactivate the prior active version for this domain with an immediate set-based UPDATE so it is ordered
        // BEFORE the tracked activate below — otherwise EF may emit the activate first (it orders same-table updates
        // by PK value, not tracking order) and the filtered unique index on (domain, is_active=1) transiently sees
        // two active rows and throws a spurious unique violation (the M7 grid HIGH-fix).
        await configurations.DeactivateActiveAsync(target.Domain, cancellationToken);
        target.Activate(actorId, clock.UtcNow);
        audit.RecordAs(ActorType.User, actorSystemLabel: null, actorUserId: actorId,
            AuditEventTypes.ConfigurationVersionActivated, AuditTargetTypes.BankConfiguration, target.Id,
            before: before, after: new { target.Domain, target.VersionNumber, target.DefinitionJson });
    }
}
