using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Identity.MakerChecker;

/// <summary>List the configured maker-checker gates (US-M1-020).</summary>
public sealed record ListMakerCheckerGatesQuery : IQuery<IReadOnlyList<MakerCheckerGateDto>>;

public sealed class ListMakerCheckerGatesQueryHandler(IMakerCheckerGateRepository gates)
    : IQueryHandler<ListMakerCheckerGatesQuery, IReadOnlyList<MakerCheckerGateDto>>
{
    public async Task<IReadOnlyList<MakerCheckerGateDto>> Handle(ListMakerCheckerGatesQuery query, CancellationToken cancellationToken)
    {
        var all = await gates.GetAllAsync(cancellationToken);
        return all.Select(g => new MakerCheckerGateDto(g.ActionType, g.IsEnabled, g.CheckerRoleName, g.AllowMakerAsChecker)).ToArray();
    }
}

/// <summary>Enable/disable a maker-checker gate and set its checker policy (US-M1-020).</summary>
public sealed record ConfigureMakerCheckerGateCommand(
    string ActionType,
    bool IsEnabled,
    string? CheckerRoleName,
    bool AllowMakerAsChecker) : ICommand<MakerCheckerGateDto>;

public sealed class ConfigureMakerCheckerGateCommandValidator : AbstractValidator<ConfigureMakerCheckerGateCommand>
{
    public ConfigureMakerCheckerGateCommandValidator() => RuleFor(x => x.ActionType).NotEmpty();
}

public sealed class ConfigureMakerCheckerGateCommandHandler(
    IMakerCheckerGateRepository gates,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ConfigureMakerCheckerGateCommand, MakerCheckerGateDto>
{
    public async Task<MakerCheckerGateDto> Handle(ConfigureMakerCheckerGateCommand command, CancellationToken cancellationToken)
    {
        var gate = await gates.GetByActionTypeAsync(command.ActionType, cancellationToken);
        if (gate is null)
        {
            gate = MakerCheckerGate.Create(command.ActionType, command.IsEnabled, command.CheckerRoleName);
            gate.Configure(command.IsEnabled, command.CheckerRoleName, command.AllowMakerAsChecker);
            gates.Add(gate);
        }
        else
        {
            gate.Configure(command.IsEnabled, command.CheckerRoleName, command.AllowMakerAsChecker);
        }

        audit.Record("maker_checker_gate_configured", "maker_checker_gate", gate.Id,
            after: new { gate.ActionType, gate.IsEnabled, gate.CheckerRoleName, gate.AllowMakerAsChecker });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new MakerCheckerGateDto(gate.ActionType, gate.IsEnabled, gate.CheckerRoleName, gate.AllowMakerAsChecker);
    }
}
