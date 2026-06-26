using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.MakerChecker;

/// <summary>
/// Decides whether an action is behind a maker-checker gate and, if so, captures it as a pending
/// action instead of executing it (US-M1-021). Does not commit — the calling handler owns the
/// transaction.
/// </summary>
public sealed class MakerCheckerGateService(
    IMakerCheckerGateRepository gates,
    IMakerCheckerRepository pendingActions,
    ICurrentUser currentUser,
    IAuditRecorder audit)
{
    /// <summary>
    /// If the action type is gated, persist a pending action and return its id; otherwise return null,
    /// signalling the caller to execute the action directly.
    /// </summary>
    public async Task<Guid?> TryCaptureAsync(
        string actionType,
        string targetObjectType,
        Guid? targetObjectId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var gate = await gates.GetByActionTypeAsync(actionType, cancellationToken);
        if (gate is not { IsEnabled: true })
        {
            return null;
        }

        var makerId = currentUser.UserId
            ?? throw new InvalidOperationException("A maker-checker action requires an authenticated maker.");

        var action = MakerCheckerAction.Submit(actionType, targetObjectType, targetObjectId, makerId, payloadJson);
        pendingActions.Add(action);
        audit.Record(AuditEventTypes.MakerCheckerSubmitted, AuditTargetTypes.MakerCheckerAction, action.Id,
            payload: new { action.ActionType, targetObjectType, targetObjectId, makerId });

        return action.Id;
    }
}
