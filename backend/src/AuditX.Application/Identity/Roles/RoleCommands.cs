using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Identity.Mapping;
using AuditX.Application.Identity.MakerChecker;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Identity.Roles;

/// <summary>Create a custom role (US-M1-012). Gated by maker-checker (<c>role_permission_change</c>) when configured.</summary>
public sealed record CreateRoleCommand(
    string Name,
    string Description,
    IReadOnlyList<RolePermissionInput> Permissions,
    IReadOnlyList<Guid> ParentRoleIds) : ICommand<RoleMutationResult>;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).ChildRules(p => p.RuleFor(i => i.Key).NotEmpty());
    }
}

public sealed class CreateRoleCommandHandler(
    RoleWriteService roleWrite,
    MakerCheckerGateService gateService,
    IPermissionResolver permissions,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRoleCommand, RoleMutationResult>
{
    public async Task<RoleMutationResult> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var data = new CreateRoleData(command.Name.Trim(), command.Description ?? string.Empty, command.Permissions, command.ParentRoleIds);
        var payload = AppJson.Serialize(new RoleChangePayload("create", data, null));

        var pendingId = await gateService.TryCaptureAsync(
            MakerCheckerActionTypes.RolePermissionChange, AuditTargetTypes.Role, null, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new RoleMutationResult(null, id);
        }

        var role = await roleWrite.ApplyCreateAsync(data, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAllAsync(cancellationToken);
        return new RoleMutationResult(role.ToDto(), null);
    }
}

/// <summary>Update a custom role's metadata, permissions and inheritance (US-M1-012/013). Maker-checker gated when configured.</summary>
public sealed record UpdateRoleCommand(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<RolePermissionInput> Permissions,
    IReadOnlyList<Guid> ParentRoleIds) : ICommand<RoleMutationResult>;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Permissions).NotNull();
    }
}

public sealed class UpdateRoleCommandHandler(
    IRoleRepository roles,
    RoleWriteService roleWrite,
    MakerCheckerGateService gateService,
    IPermissionResolver permissions,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRoleCommand, RoleMutationResult>
{
    public async Task<RoleMutationResult> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        // Reject built-in edits up front for a clear error (US-M1-011), independent of the gate.
        var existing = await roles.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Role", command.Id);
        if (existing.IsBuiltIn)
        {
            throw new ConflictException("role_builtin_immutable", "Built-in roles cannot be modified; clone into a custom role instead.");
        }

        var data = new UpdateRoleData(command.Id, command.Name.Trim(), command.Description ?? string.Empty, command.Permissions, command.ParentRoleIds);
        var payload = AppJson.Serialize(new RoleChangePayload("update", null, data));

        var pendingId = await gateService.TryCaptureAsync(
            MakerCheckerActionTypes.RolePermissionChange, AuditTargetTypes.Role, command.Id, payload, cancellationToken);
        if (pendingId is { } id)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new RoleMutationResult(null, id);
        }

        var role = await roleWrite.ApplyUpdateAsync(data, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAllAsync(cancellationToken);
        return new RoleMutationResult(role.ToDto(), null);
    }
}

/// <summary>Archive a custom role (US-M1-019).</summary>
public sealed record ArchiveRoleCommand(Guid Id) : ICommand<Unit>;

public sealed class ArchiveRoleCommandHandler(
    IRoleRepository roles,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveRoleCommand, Unit>
{
    public async Task<Unit> Handle(ArchiveRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await roles.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Role", command.Id);
        role.Archive();
        audit.Record(AuditEventTypes.RoleArchived, AuditTargetTypes.Role, role.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissions.InvalidateAllAsync(cancellationToken);
        return Unit.Value;
    }
}
