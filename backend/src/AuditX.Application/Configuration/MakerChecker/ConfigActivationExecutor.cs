using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Configuration.Commands;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Configuration.MakerChecker;

/// <summary>
/// Replays an approved <c>config_activation</c> action — activating the configuration version the maker requested —
/// performing the atomic active switch inside the approval transaction (M12, S6). Attributed to the maker. Used for
/// both plain activation and rollback (rollback's NEW forward version is what gets captured as the pending target).
/// The active-config cache is invalidated post-commit by the generic approval handler, not here.
/// </summary>
public sealed class ConfigActivationExecutor(
    IBankConfigurationRepository configurations, IAuditRecorder audit, IClock clock)
    : IPendingActionExecutor
{
    public string ActionType => MakerCheckerActionTypes.ConfigActivation;

    public async Task ExecuteAsync(string pendingPayloadJson, Guid makerUserId, CancellationToken cancellationToken)
    {
        var payload = AppJson.Deserialize<ConfigActivationPayload>(pendingPayloadJson);
        var target = await configurations.GetByIdAsync(payload.ConfigurationId, cancellationToken)
            ?? throw new NotFoundException("Configuration version", payload.ConfigurationId);

        await ConfigurationActivation.SwitchAsync(configurations, target, makerUserId, audit, clock, cancellationToken);

        // A rollback's switch is just an activation; emit the rollback-provenance trail here so the gated path records
        // it too (the direct path records it in the command handler).
        if (payload.RolledBackFromVersion is { } fromVersion)
        {
            audit.RecordAs(ActorType.User, actorSystemLabel: null, actorUserId: makerUserId,
                AuditEventTypes.ConfigurationRolledBack, AuditTargetTypes.BankConfiguration, target.Id,
                payload: new { target.Domain, fromVersion, newVersion = target.VersionNumber });
        }
    }
}
