using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.MakerChecker;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Application.Sanctions.Dtos;
using AuditX.Application.Sanctions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Identity;
using AuditX.Domain.Sanctions;
using FluentValidation;

namespace AuditX.Application.Sanctions.Commands;

public sealed record CreateGridVersionCommand(string GridDefinitionJson) : ICommand<SanctionsGridActionResult>;

public sealed class CreateGridVersionCommandValidator : AbstractValidator<CreateGridVersionCommand>
{
    public CreateGridVersionCommandValidator() => RuleFor(x => x.GridDefinitionJson).NotEmpty();
}

public sealed class CreateGridVersionCommandHandler(
    ISanctionsGridRepository grids, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateGridVersionCommand, SanctionsGridActionResult>
{
    public async Task<SanctionsGridActionResult> Handle(CreateGridVersionCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        GridConsultation.ValidateShape(command.GridDefinitionJson);

        var next = await grids.GetMaxVersionNumberAsync(cancellationToken) + 1;
        var draft = SanctionsGridVersion.CreateDraft(next, command.GridDefinitionJson, actorId, clock.UtcNow);
        grids.Add(draft);
        audit.Record(AuditEventTypes.GridVersionCreated, AuditTargetTypes.SanctionsGridVersion, draft.Id, after: new { draft.VersionNumber });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SanctionsGridActionResult(draft.ToDto(), null);
    }
}

/// <summary>The serialized intent captured when grid activation is held by the maker-checker gate.</summary>
public sealed record GridActivationPayload(Guid GridVersionId, string ActivationReason);

public sealed record ActivateGridVersionCommand(Guid GridVersionId, string ActivationReason) : ICommand<SanctionsGridActionResult>;

public sealed class ActivateGridVersionCommandValidator : AbstractValidator<ActivateGridVersionCommand>
{
    public ActivateGridVersionCommandValidator()
    {
        RuleFor(x => x.GridVersionId).NotEmpty();
        RuleFor(x => x.ActivationReason).NotEmpty().MinimumLength(20);
    }
}

public sealed class ActivateGridVersionCommandHandler(
    ISanctionsGridRepository grids, MakerCheckerGateService gateService, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateGridVersionCommand, SanctionsGridActionResult>
{
    public async Task<SanctionsGridActionResult> Handle(ActivateGridVersionCommand command, CancellationToken cancellationToken)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedException();
        var grid = await grids.GetByIdAsync(command.GridVersionId, cancellationToken) ?? throw new NotFoundException("Sanctions grid version", command.GridVersionId);
        if (grid.IsActive)
        {
            throw new ConflictException("sanctions.grid_already_active", "This grid version is already active.");
        }

        // Activation is the gated policy change (maker-checker on activate only). The executor performs the atomic switch.
        var payload = AppJson.Serialize(new GridActivationPayload(grid.Id, command.ActivationReason));
        var pendingId = await gateService.TryCaptureAsync(MakerCheckerActionTypes.SanctionsGridEdit, AuditTargetTypes.SanctionsGridVersion, grid.Id, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new SanctionsGridActionResult(null, id);
        }

        await SanctionsGridActivation.SwitchAsync(grids, grid, command.ActivationReason, currentUser.UserId!.Value, audit, clock, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SanctionsGridActionResult(grid.ToDto(), null);
    }
}

/// <summary>Shared atomic active-grid switch used both by the direct path and the maker-checker executor.</summary>
internal static class SanctionsGridActivation
{
    public static async Task SwitchAsync(
        ISanctionsGridRepository grids, SanctionsGridVersion grid, string activationReason, Guid actorId,
        IAuditRecorder audit, IClock clock, CancellationToken cancellationToken)
    {
        // Deactivate the prior active version with an immediate set-based UPDATE so it is ordered BEFORE the
        // tracked activate below. Otherwise EF may emit the activate UPDATE first (it orders same-table updates
        // by PK value, not tracking order), and the filtered unique index on is_active=1 transiently sees two
        // active rows and throws a spurious unique violation.
        await grids.DeactivateActiveAsync(cancellationToken);
        grid.Activate(activationReason, actorId, clock.UtcNow);
        audit.Record(AuditEventTypes.GridVersionActivated, AuditTargetTypes.SanctionsGridVersion, grid.Id, after: new { grid.VersionNumber, activationReason });
    }
}
