using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Sanctions.Commands;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Application.Sanctions.MakerChecker;

/// <summary>
/// Replays an approved <c>sanctions_grid_edit</c> action — activating the grid version the maker requested — and
/// performs the atomic active-grid switch inside the approval transaction (US-M7-002/003). Attributed to the maker.
/// </summary>
public sealed class SanctionsGridEditExecutor(ISanctionsGridRepository grids, IAuditRecorder audit, IClock clock) : IPendingActionExecutor
{
    public string ActionType => MakerCheckerActionTypes.SanctionsGridEdit;

    public async Task ExecuteAsync(string pendingPayloadJson, Guid makerUserId, CancellationToken cancellationToken)
    {
        var payload = AppJson.Deserialize<GridActivationPayload>(pendingPayloadJson);
        var grid = await grids.GetByIdAsync(payload.GridVersionId, cancellationToken)
            ?? throw new NotFoundException("Sanctions grid version", payload.GridVersionId);

        // Set-based deactivate ordered before the tracked activate (see SanctionsGridActivation.SwitchAsync) so
        // the filtered unique index on is_active=1 never momentarily sees two active rows during the switch.
        await grids.DeactivateActiveAsync(cancellationToken);
        grid.Activate(payload.ActivationReason, makerUserId, clock.UtcNow);
        audit.RecordAs(ActorType.User, actorSystemLabel: null, actorUserId: makerUserId,
            AuditEventTypes.GridVersionActivated, AuditTargetTypes.SanctionsGridVersion, grid.Id, after: new { grid.VersionNumber, payload.ActivationReason });
    }
}
