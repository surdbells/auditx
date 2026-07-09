using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Universe.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Organization;
using FluentValidation;

namespace AuditX.Application.Organization;

// ---- Create ----

public sealed record CreateOrgUnitCommand(string Name, string Code, Guid? ParentOrgUnitId) : ICommand<OrgUnitDto>;

public sealed class CreateOrgUnitCommandValidator : AbstractValidator<CreateOrgUnitCommand>
{
    public CreateOrgUnitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
    }
}

public sealed class CreateOrgUnitCommandHandler(IOrgUnitRepository orgUnits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOrgUnitCommand, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(CreateOrgUnitCommand command, CancellationToken cancellationToken)
    {
        if (await orgUnits.CodeExistsAsync(command.Code, null, cancellationToken))
        {
            throw new ConflictException("org_unit.code_taken", $"Org-unit code '{command.Code}' is already in use.");
        }

        if (command.ParentOrgUnitId is { } parentId && await orgUnits.GetByIdAsync(parentId, cancellationToken) is null)
        {
            throw new NotFoundException("OrgUnit", parentId);
        }

        var orgUnit = OrgUnit.Create(command.Name, command.Code, command.ParentOrgUnitId);
        orgUnits.Add(orgUnit);
        audit.Record(AuditEventTypes.OrgUnitCreated, AuditTargetTypes.OrgUnit, orgUnit.Id,
            after: new { orgUnit.Name, orgUnit.Code, orgUnit.ParentOrgUnitId });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrgUnitDto(orgUnit.Id, orgUnit.Name, orgUnit.Code, orgUnit.ParentOrgUnitId, orgUnit.IsArchived);
    }
}

// ---- Rename ----

public sealed record RenameOrgUnitCommand(Guid Id, string Name) : ICommand<OrgUnitDto>;

public sealed class RenameOrgUnitCommandValidator : AbstractValidator<RenameOrgUnitCommand>
{
    public RenameOrgUnitCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public sealed class RenameOrgUnitCommandHandler(IOrgUnitRepository orgUnits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RenameOrgUnitCommand, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(RenameOrgUnitCommand command, CancellationToken cancellationToken)
    {
        var orgUnit = await orgUnits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("OrgUnit", command.Id);
        orgUnit.Rename(command.Name);
        audit.Record(AuditEventTypes.OrgUnitUpdated, AuditTargetTypes.OrgUnit, orgUnit.Id, after: new { orgUnit.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new OrgUnitDto(orgUnit.Id, orgUnit.Name, orgUnit.Code, orgUnit.ParentOrgUnitId, orgUnit.IsArchived);
    }
}

// ---- Reparent (acyclic) ----

public sealed record ReparentOrgUnitCommand(Guid Id, Guid? ParentOrgUnitId) : ICommand<OrgUnitDto>;

public sealed class ReparentOrgUnitCommandHandler(IOrgUnitRepository orgUnits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReparentOrgUnitCommand, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(ReparentOrgUnitCommand command, CancellationToken cancellationToken)
    {
        var orgUnit = await orgUnits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("OrgUnit", command.Id);

        if (command.ParentOrgUnitId is { } parentId)
        {
            if (await orgUnits.GetByIdAsync(parentId, cancellationToken) is null)
            {
                throw new NotFoundException("OrgUnit", parentId);
            }

            var parentMap = (await orgUnits.GetAllAsync(includeArchived: true, cancellationToken))
                .ToDictionary(o => o.Id, o => o.ParentOrgUnitId);
            if (HierarchyGuard.WouldCreateCycle(parentMap, command.Id, parentId))
            {
                throw new ConflictException("org_unit.cycle_detected", "Re-parenting there would create a cycle in the org hierarchy.");
            }
        }

        orgUnit.SetParent(command.ParentOrgUnitId);
        audit.Record(AuditEventTypes.OrgUnitUpdated, AuditTargetTypes.OrgUnit, orgUnit.Id, after: new { orgUnit.ParentOrgUnitId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new OrgUnitDto(orgUnit.Id, orgUnit.Name, orgUnit.Code, orgUnit.ParentOrgUnitId, orgUnit.IsArchived);
    }
}

// ---- Archive / Restore ----

public sealed record SetOrgUnitArchivedCommand(Guid Id, bool Archived) : ICommand<OrgUnitDto>;

public sealed class SetOrgUnitArchivedCommandHandler(IOrgUnitRepository orgUnits, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetOrgUnitArchivedCommand, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(SetOrgUnitArchivedCommand command, CancellationToken cancellationToken)
    {
        var orgUnit = await orgUnits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("OrgUnit", command.Id);
        if (command.Archived)
        {
            orgUnit.Archive();
        }
        else
        {
            orgUnit.Restore();
        }

        audit.Record(AuditEventTypes.OrgUnitArchived, AuditTargetTypes.OrgUnit, orgUnit.Id, after: new { orgUnit.IsArchived });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new OrgUnitDto(orgUnit.Id, orgUnit.Name, orgUnit.Code, orgUnit.ParentOrgUnitId, orgUnit.IsArchived);
    }
}
