using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Organization;
using AuditX.Domain.Organization;
using NSubstitute;

namespace AuditX.Application.Tests.Organization;

public sealed class ReparentOrgUnitCommandTests
{
    private static OrgUnit Node(string name, string code, Guid? parent)
    {
        var o = OrgUnit.Create(name, code, parent);
        return o;
    }

    [Fact]
    public async Task Rejects_a_reparent_that_would_create_a_cycle()
    {
        // a → b → c (c child of b, b child of a). Making 'a' a child of 'c' would cycle.
        var a = Node("A", "A", null);
        var b = Node("B", "B", a.Id);
        var c = Node("C", "C", b.Id);

        var repo = Substitute.For<IOrgUnitRepository>();
        repo.GetByIdAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        repo.GetByIdAsync(c.Id, Arg.Any<CancellationToken>()).Returns(c);
        repo.GetAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(new[] { a, b, c });

        var handler = new ReparentOrgUnitCommandHandler(repo, Substitute.For<IAuditRecorder>(), Substitute.For<IUnitOfWork>());

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new ReparentOrgUnitCommand(a.Id, c.Id), default));
    }

    [Fact]
    public async Task Allows_a_valid_reparent()
    {
        var a = Node("A", "A", null);
        var b = Node("B", "B", null);

        var repo = Substitute.For<IOrgUnitRepository>();
        repo.GetByIdAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        repo.GetByIdAsync(b.Id, Arg.Any<CancellationToken>()).Returns(b);
        repo.GetAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(new[] { a, b });

        var uow = Substitute.For<IUnitOfWork>();
        var handler = new ReparentOrgUnitCommandHandler(repo, Substitute.For<IAuditRecorder>(), uow);

        var result = await handler.Handle(new ReparentOrgUnitCommand(a.Id, b.Id), default);

        Assert.Equal(b.Id, result.ParentOrgUnitId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
