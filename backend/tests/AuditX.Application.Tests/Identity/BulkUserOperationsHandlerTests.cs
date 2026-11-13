using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Administration.Commands;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using NSubstitute;

namespace AuditX.Application.Tests.Identity;

public sealed class BulkUserOperationsHandlerTests
{
    private static User Deactivated(string sam)
    {
        var user = User.ProvisionFromDirectory(sam, $"{sam}@bank.local", $"S-1-5-21-{sam}", $"{sam}@bank.local", sam, "User");
        user.Deactivate(null);
        return user;
    }

    private static User Active(string sam)
    {
        var user = User.ProvisionFromDirectory(sam, $"{sam}@bank.local", $"S-1-5-21-{sam}", $"{sam}@bank.local", sam, "User");
        user.MarkActiveOnFirstRole();
        return user;
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    /// <summary>A role repository where each listed user holds the given grants (everyone else holds none).</summary>
    private static IUserRoleRepository RolesFor(params (User User, UserRole[] Grants)[] holdings)
    {
        var userRoles = Substitute.For<IUserRoleRepository>();
        userRoles.GetForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        foreach (var (user, grants) in holdings)
        {
            userRoles.GetForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(grants);
        }

        return userRoles;
    }

    [Fact]
    public async Task BulkReactivate_reactivates_all_found_users_and_invalidates_permissions()
    {
        var a = Deactivated("alpha");
        var b = Deactivated("bravo");

        var users = Substitute.For<IUserRepository>();
        users.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([a, b]);
        var permissions = Substitute.For<IPermissionResolver>();
        var audit = Substitute.For<IAuditRecorder>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = new BulkReactivateUsersCommandHandler(users, RolesFor(), permissions, audit, uow, ClockAt(Now));

        var result = await handler.Handle(new BulkReactivateUsersCommand([a.Id, b.Id]), CancellationToken.None);

        Assert.Equal(2, result.SuccessCount);
        Assert.Empty(result.Errors);
        // Neither holds a role → both wait for an administrator to assign one.
        Assert.Equal(UserStatus.AwaitingRoleAssignment, a.Status);
        Assert.Equal(UserStatus.AwaitingRoleAssignment, b.Status);
        audit.Received(1).Record(AuditEventTypes.UsersBulkReactivated, Arg.Any<string>(), null,
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await permissions.Received(1).InvalidateAsync(a.Id, Arg.Any<CancellationToken>());
        await permissions.Received(1).InvalidateAsync(b.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkReactivate_returns_a_user_who_still_holds_a_role_to_active_not_awaiting_role()
    {
        // Deactivation keeps role grants. Sending a role-holder back to "awaiting role" locked them out of the app,
        // and the fix an administrator would reach for — granting the role — was refused as "already holds it".
        var roleHolder = Deactivated("alpha");
        var noRoles = Deactivated("bravo");
        var expiredDelegate = Deactivated("charlie");
        var roleId = Guid.NewGuid();

        var users = Substitute.For<IUserRepository>();
        users.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([roleHolder, noRoles, expiredDelegate]);
        var userRoles = RolesFor(
            (roleHolder, [UserRole.Grant(roleHolder.Id, roleId)]),
            // A delegation whose window has passed confers nothing, so it does not count as holding a role.
            (expiredDelegate, [UserRole.Delegate(expiredDelegate.Id, roleId, Guid.NewGuid(), Now.AddDays(-10), Now.AddDays(-3))]));

        var handler = new BulkReactivateUsersCommandHandler(
            users, userRoles, Substitute.For<IPermissionResolver>(), Substitute.For<IAuditRecorder>(), Substitute.For<IUnitOfWork>(), ClockAt(Now));

        var result = await handler.Handle(
            new BulkReactivateUsersCommand([roleHolder.Id, noRoles.Id, expiredDelegate.Id]), CancellationToken.None);

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(UserStatus.Active, roleHolder.Status);
        Assert.Equal(UserStatus.AwaitingRoleAssignment, noRoles.Status);
        Assert.Equal(UserStatus.AwaitingRoleAssignment, expiredDelegate.Status);
    }

    [Fact]
    public async Task BulkReactivate_is_atomic_when_a_user_is_missing()
    {
        var a = Deactivated("alpha");
        var missing = Guid.NewGuid();

        var users = Substitute.For<IUserRepository>();
        users.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([a]);
        var uow = Substitute.For<IUnitOfWork>();

        var handler = new BulkReactivateUsersCommandHandler(
            users, RolesFor(), Substitute.For<IPermissionResolver>(), Substitute.For<IAuditRecorder>(), uow, ClockAt(Now));

        var result = await handler.Handle(new BulkReactivateUsersCommand([a.Id, missing]), CancellationToken.None);

        Assert.Equal(0, result.SuccessCount);
        Assert.Single(result.Errors);
        Assert.Equal(missing.ToString(), result.Errors[0].Identifier);
        Assert.Equal(UserStatus.Deactivated, a.Status); // untouched — nothing committed
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkDeactivate_deactivates_all_found_users()
    {
        var a = Active("alpha");
        var b = Active("bravo");

        var users = Substitute.For<IUserRepository>();
        users.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([a, b]);
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        var permissions = Substitute.For<IPermissionResolver>();
        var audit = Substitute.For<IAuditRecorder>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = new BulkDeactivateUsersCommandHandler(users, currentUser, permissions, audit, uow);

        var result = await handler.Handle(new BulkDeactivateUsersCommand([a.Id, b.Id]), CancellationToken.None);

        Assert.Equal(2, result.SuccessCount);
        Assert.Empty(result.Errors);
        Assert.Equal(UserStatus.Deactivated, a.Status);
        Assert.Equal(UserStatus.Deactivated, b.Status);
        audit.Received(1).Record(AuditEventTypes.UsersBulkDeactivated, Arg.Any<string>(), null,
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
