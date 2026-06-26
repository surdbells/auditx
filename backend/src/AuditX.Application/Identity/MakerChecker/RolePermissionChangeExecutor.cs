using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Identity.Roles;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.MakerChecker;

/// <summary>
/// Replays an approved <c>role_permission_change</c> action — the create or update the maker
/// originally requested — attributing it to the maker. Runs inside the approval transaction.
/// </summary>
public sealed class RolePermissionChangeExecutor(RoleWriteService roleWrite) : IPendingActionExecutor
{
    public string ActionType => MakerCheckerActionTypes.RolePermissionChange;

    public async Task ExecuteAsync(string pendingPayloadJson, Guid makerUserId, CancellationToken cancellationToken)
    {
        var payload = AppJson.Deserialize<RoleChangePayload>(pendingPayloadJson);
        switch (payload.Operation)
        {
            case "create" when payload.Create is not null:
                await roleWrite.ApplyCreateAsync(payload.Create, makerUserId, cancellationToken);
                break;
            case "update" when payload.Update is not null:
                await roleWrite.ApplyUpdateAsync(payload.Update, makerUserId, cancellationToken);
                break;
            default:
                throw new ConflictException("mc.invalid_payload", "The pending role change payload is invalid.");
        }
    }
}
