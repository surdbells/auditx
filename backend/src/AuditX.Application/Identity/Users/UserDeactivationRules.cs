using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Models;
using AuditX.Domain.Authorization;
using AuditX.Domain.Enums;

namespace AuditX.Application.Identity.Users;

/// <summary>
/// Who may NOT be deactivated: the caller themselves (it ends their own session on the spot, and nobody can then undo
/// it from that account), and the last active administrator (it would leave nobody able to administer the platform).
/// </summary>
internal static class UserDeactivationRules
{
    public const string SelfMessage = "You cannot deactivate your own account. Ask another administrator to do it.";
    public const string LastAdministratorMessage =
        "This is the last active administrator. Give another person the administrator role before deactivating this account.";

    /// <summary>The reason each of <paramref name="targetIds"/> may not be deactivated (empty when all may be).</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> RefusalsAsync(
        IReadOnlyCollection<Guid> targetIds, Guid? callerId, IUserRepository users, CancellationToken cancellationToken)
    {
        var refusals = new Dictionary<Guid, string>();
        if (callerId is { } caller && targetIds.Contains(caller))
        {
            refusals[caller] = SelfMessage;
        }

        var administrators = await users.SearchAsync(
            null, BuiltInRoles.AdministratorName, UserStatus.Active, PageSpec.Of(1, 0), cancellationToken);
        var activeAdmins = administrators.Items.Select(u => u.Id).ToHashSet();
        var remaining = activeAdmins.Where(id => !targetIds.Contains(id)).ToArray();
        if (remaining.Length == 0)
        {
            foreach (var id in targetIds.Where(activeAdmins.Contains))
            {
                refusals.TryAdd(id, LastAdministratorMessage);
            }
        }

        return refusals;
    }
}
