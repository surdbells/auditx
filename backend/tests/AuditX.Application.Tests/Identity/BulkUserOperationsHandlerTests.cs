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

        var handler = new BulkReactivateUsersCommandHandler(users, permissions, audit, uow);

        var result = await handler.Handle(new BulkReactivateUsersCommand([a.Id, b.Id]), CancellationToken.None);

        Assert.Equal(2, result.SuccessCount);
        Assert.Empty(result.Errors);
        Assert.Equal(UserStatus.AwaitingRoleAssignment, a.Status);
        Assert.Equal(UserStatus.AwaitingRoleAssignment, b.Status);
        audit.Received(1).Record(AuditEventTypes.UsersBulkReactivated, Arg.Any<string>(), null,
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await permissions.Received(1).InvalidateAsync(a.Id, Arg.Any<CancellationToken>());
        await permissions.Received(1).InvalidateAsync(b.Id, Arg.Any<CancellationToken>());
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
            users, Substitute.For<IPermissionResolver>(), Substitute.For<IAuditRecorder>(), uow);

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
