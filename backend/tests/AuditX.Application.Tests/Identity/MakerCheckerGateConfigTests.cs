using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Domain.Identity;
using NSubstitute;

namespace AuditX.Application.Tests.Identity;

public sealed class MakerCheckerGateConfigTests
{
    [Fact]
    public async Task List_returns_every_enforced_type_overlaying_persisted_rows()
    {
        // One persisted row disables config_activation; the rest have no row and fall back to the default (enabled).
        var stored = MakerCheckerGate.Create(MakerCheckerActionTypes.ConfigActivation, isEnabled: false);
        var gates = Substitute.For<IMakerCheckerGateRepository>();
        gates.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { stored });

        var handler = new ListMakerCheckerGatesQueryHandler(gates);
        var result = await handler.Handle(new ListMakerCheckerGatesQuery(), CancellationToken.None);

        // Exactly the enforced catalogue, in order — placeholder types are excluded.
        Assert.Equal(MakerCheckerActionCatalogue.Enforced, result.Select(r => r.ActionType).ToArray());
        Assert.False(result.Single(r => r.ActionType == MakerCheckerActionTypes.ConfigActivation).IsEnabled);
        Assert.True(result.Single(r => r.ActionType == MakerCheckerActionTypes.RolePermissionChange).IsEnabled);
    }

    [Fact]
    public void Validator_rejects_non_enforced_action_types()
    {
        var validator = new ConfigureMakerCheckerGateCommandValidator();

        // A declared-but-not-yet-wired placeholder must be refused.
        var bad = validator.Validate(new ConfigureMakerCheckerGateCommand(
            MakerCheckerActionTypes.PlanApproval, IsEnabled: true, CheckerRoleName: null, AllowMakerAsChecker: false));
        Assert.False(bad.IsValid);

        var ok = validator.Validate(new ConfigureMakerCheckerGateCommand(
            MakerCheckerActionTypes.TemplatePublish, IsEnabled: false, CheckerRoleName: null, AllowMakerAsChecker: false));
        Assert.True(ok.IsValid);
    }

    [Fact]
    public async Task Configure_creates_a_gate_when_none_exists_and_audits()
    {
        var gates = Substitute.For<IMakerCheckerGateRepository>();
        gates.GetByActionTypeAsync(MakerCheckerActionTypes.TemplatePublish, Arg.Any<CancellationToken>())
            .Returns((MakerCheckerGate?)null);
        var audit = Substitute.For<IAuditRecorder>();
        var uow = Substitute.For<IUnitOfWork>();

        var handler = new ConfigureMakerCheckerGateCommandHandler(gates, audit, uow);
        var result = await handler.Handle(new ConfigureMakerCheckerGateCommand(
            MakerCheckerActionTypes.TemplatePublish, IsEnabled: true, CheckerRoleName: "Audit Manager", AllowMakerAsChecker: false),
            CancellationToken.None);

        Assert.Equal(MakerCheckerActionTypes.TemplatePublish, result.ActionType);
        Assert.True(result.IsEnabled);
        Assert.Equal("Audit Manager", result.CheckerRoleName);
        gates.Received(1).Add(Arg.Any<MakerCheckerGate>());
        audit.Received(1).Record("maker_checker_gate_configured", "maker_checker_gate", Arg.Any<Guid>(),
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Configure_updates_an_existing_gate_without_adding()
    {
        var existing = MakerCheckerGate.Create(MakerCheckerActionTypes.MapApproval, isEnabled: true);
        var gates = Substitute.For<IMakerCheckerGateRepository>();
        gates.GetByActionTypeAsync(MakerCheckerActionTypes.MapApproval, Arg.Any<CancellationToken>()).Returns(existing);
        var uow = Substitute.For<IUnitOfWork>();

        var handler = new ConfigureMakerCheckerGateCommandHandler(gates, Substitute.For<IAuditRecorder>(), uow);
        var result = await handler.Handle(new ConfigureMakerCheckerGateCommand(
            MakerCheckerActionTypes.MapApproval, IsEnabled: false, CheckerRoleName: null, AllowMakerAsChecker: true),
            CancellationToken.None);

        Assert.False(result.IsEnabled);
        Assert.True(result.AllowMakerAsChecker);
        gates.DidNotReceive().Add(Arg.Any<MakerCheckerGate>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
