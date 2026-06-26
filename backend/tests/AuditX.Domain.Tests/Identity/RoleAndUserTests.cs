using AuditX.Domain.Authorization;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Domain.Tests.Identity;

public sealed class RoleAndUserTests
{
    [Fact]
    public void ProvisionFromDirectory_creates_awaiting_role_user_with_no_roles()
    {
        var user = User.ProvisionFromDirectory("jdoe", "jdoe@bank.local", "S-1-5-21-1", "jdoe@bank.local", "John", "Doe");

        Assert.Equal(UserStatus.AwaitingRoleAssignment, user.Status);
        Assert.Contains(user.DomainEvents, e => e is Domain.Identity.Events.UserProvisionedEvent);
    }

    [Fact]
    public void MarkActiveOnFirstRole_activates_awaiting_user()
    {
        var user = User.ProvisionFromDirectory("jdoe", "jdoe@bank.local", "S-1-5-21-1", "jdoe@bank.local", "John", "Doe");

        user.MarkActiveOnFirstRole();

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Deactivated_user_cannot_record_login()
    {
        var user = User.ProvisionFromDirectory("jdoe", "jdoe@bank.local", "S-1-5-21-1", "jdoe@bank.local", "John", "Doe");
        user.Deactivate(null);

        Assert.Throws<DomainException>(() => user.RecordLogin(DateTimeOffset.UnixEpoch, AuthenticationMethod.Forms));
    }

    [Fact]
    public void BuiltIn_role_cannot_be_modified()
    {
        var role = Role.CreateBuiltIn(BuiltInRoles.Auditor);

        Assert.Throws<DomainException>(() => role.Rename("X", "Y"));
        Assert.Throws<DomainException>(() => role.Archive());
    }

    [Fact]
    public void Custom_role_rejects_unknown_permission_key()
    {
        var role = Role.CreateCustom("Regional Lead", "desc");

        Assert.Throws<DomainException>(() =>
            role.SetPermissions([("NotARealPermission", PermissionScopeType.Global, null)]));
    }

    [Fact]
    public void Custom_role_accepts_catalogue_permissions()
    {
        var role = Role.CreateCustom("Regional Lead", "desc");

        role.SetPermissions([(PermissionKeys.CloseException, PermissionScopeType.Audit, null)]);

        Assert.Single(role.Permissions);
        Assert.Equal(PermissionKeys.CloseException, role.Permissions[0].PermissionKey);
    }

    [Fact]
    public void Role_cannot_inherit_from_itself()
    {
        var role = Role.CreateCustom("A", "desc");

        Assert.Throws<DomainException>(() => role.SetParents([role.Id]));
    }
}

public sealed class UserRoleDelegationTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;
    private static readonly DateTimeOffset End = Start.AddDays(14);

    [Fact]
    public void Delegation_requires_end_after_start()
        => Assert.Throws<DomainException>(() => UserRole.Delegate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), End, Start));

    [Fact]
    public void Delegation_is_effective_within_window_only()
    {
        var delegation = UserRole.Delegate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, End);

        Assert.False(delegation.IsEffectiveAt(Start.AddDays(-1)));
        Assert.True(delegation.IsEffectiveAt(Start.AddDays(1)));
        Assert.False(delegation.IsEffectiveAt(End.AddDays(1)));
    }

    [Fact]
    public void ExpireIfElapsed_deactivates_after_window()
    {
        var delegation = UserRole.Delegate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, End);

        Assert.True(delegation.ExpireIfElapsed(End.AddSeconds(1)));
        Assert.False(delegation.IsActive);
        Assert.False(delegation.ExpireIfElapsed(End.AddSeconds(2)));
    }

    [Fact]
    public void Permanent_grant_is_always_effective_until_deactivated()
    {
        var grant = UserRole.Grant(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(grant.IsEffectiveAt(DateTimeOffset.UnixEpoch));
        grant.Deactivate();
        Assert.False(grant.IsEffectiveAt(DateTimeOffset.UnixEpoch));
    }
}
