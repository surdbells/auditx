using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Identity;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Identity.Authentication;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Domain.Identity;
using NSubstitute;

namespace AuditX.Application.Tests.Identity;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Invalid_credentials_record_failure_and_throw_opaque_unauthorized()
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.AuthenticateAsync("jdoe", "wrong", Arg.Any<CancellationToken>()).Returns((DirectoryUser?)null);
        var users = Substitute.For<IUserRepository>();
        users.GetByUsernameAsync("jdoe", Arg.Any<CancellationToken>()).Returns((User?)null); // no local user → directory path
        var audit = Substitute.For<IAuditRecorder>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = new LoginCommandHandler(identity, users, localAuthenticator: null!, sessionService: null!, audit, uow);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("jdoe", "wrong"), CancellationToken.None));

        Assert.Equal("invalid_credentials", ex.ErrorCode);
        audit.Received(1).RecordAs(
            Arg.Any<Domain.Enums.ActorType>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(),
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class ApproveActionCommandHandlerTests
{
    [Fact]
    public async Task Maker_cannot_approve_their_own_action()
    {
        var maker = Guid.NewGuid();
        var action = MakerCheckerAction.Submit("role_permission_change", "role", null, maker, "{}");

        var repo = Substitute.For<IMakerCheckerRepository>();
        repo.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        var executor = Substitute.For<IPendingActionExecutor>();
        executor.ActionType.Returns("role_permission_change");

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(maker);

        var handler = new ApproveActionCommandHandler(
            repo, [executor], currentUser, Substitute.For<IPermissionResolver>(),
            Substitute.For<IActiveConfigurationProvider>(),
            Substitute.For<IAuditRecorder>(), Substitute.For<IClock>(), Substitute.For<IUnitOfWork>());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(new ApproveActionCommand(action.Id), CancellationToken.None));

        await executor.DidNotReceive().ExecuteAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approving_missing_action_throws_not_found()
    {
        var repo = Substitute.For<IMakerCheckerRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MakerCheckerAction?)null);
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());

        var handler = new ApproveActionCommandHandler(
            repo, [], currentUser, Substitute.For<IPermissionResolver>(),
            Substitute.For<IActiveConfigurationProvider>(),
            Substitute.For<IAuditRecorder>(), Substitute.For<IClock>(), Substitute.For<IUnitOfWork>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ApproveActionCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
