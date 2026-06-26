using AuditX.Domain.Common;

namespace AuditX.Domain.Identity;

/// <summary>
/// Assignment of a role to a user, optionally narrowed by <see cref="ScopeValue"/> (e.g. a specific
/// audit id for an audit-scoped grant). The same row type also models a <em>delegation</em>: a
/// time-bounded grant where <see cref="DelegatedFromUserId"/> identifies the delegator and the grant
/// is only effective within [<see cref="DelegationStart"/>, <see cref="DelegationEnd"/>].
/// </summary>
public sealed class UserRole : Entity
{
    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    /// <summary>Optional resource id this grant is scoped to (null = the role's intrinsic scopes apply).</summary>
    public string? ScopeValue { get; private set; }

    public Guid? DelegatedFromUserId { get; private set; }

    public DateTimeOffset? DelegationStart { get; private set; }

    public DateTimeOffset? DelegationEnd { get; private set; }

    /// <summary>Set false by early termination or by the hourly expiry job once the window has passed.</summary>
    public bool IsActive { get; private set; } = true;

    public bool IsDelegation => DelegatedFromUserId.HasValue;

    public static UserRole Grant(Guid userId, Guid roleId, string? scopeValue = null)
    {
        return new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            ScopeValue = scopeValue,
            IsActive = true,
        };
    }

    public static UserRole Delegate(Guid toUserId, Guid roleId, Guid fromUserId, DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainException("delegation.invalid_window", "Delegation end must be after its start.");
        }

        return new UserRole
        {
            UserId = toUserId,
            RoleId = roleId,
            DelegatedFromUserId = fromUserId,
            DelegationStart = start,
            DelegationEnd = end,
            IsActive = true,
        };
    }

    /// <summary>True if this grant currently confers permissions at the given instant.</summary>
    public bool IsEffectiveAt(DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            return false;
        }

        if (!IsDelegation)
        {
            return true;
        }

        return DelegationStart <= nowUtc && nowUtc < DelegationEnd;
    }

    /// <summary>Deactivate the grant (early delegation termination, US-M1-029).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Mark a delegation inactive because its window has elapsed (US-M1-028).</summary>
    public bool ExpireIfElapsed(DateTimeOffset nowUtc)
    {
        if (IsDelegation && IsActive && DelegationEnd <= nowUtc)
        {
            IsActive = false;
            return true;
        }

        return false;
    }
}
