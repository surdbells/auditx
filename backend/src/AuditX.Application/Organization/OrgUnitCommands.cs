using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Universe.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Organization;
using FluentValidation;

namespace AuditX.Application.Organization;

// ---- Create ----

public sealed record CreateOrgUnitCommand(string Name, string Code, Guid? ParentOrgUnitId, string? UnitType = null) : ICommand<OrgUnitDto>;

internal static class OrgUnitTypeRules
{
    /// <summary>A unit type, when given, must be a live value of the org-unit type reference list.</summary>
    public static async Task EnsureKnownAsync(IReferenceDataRepository referenceData, string? unitType, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(unitType)
            && !await referenceData.ExistsAsync(Domain.ReferenceData.ReferenceDataCategories.OrgUnitType, unitType.Trim(), cancellationToken))
        {
            throw new DomainException("org_unit.unknown_type", $"Unknown org-unit type '{unitType}'. Choose one from the list (Configuration › Reference data › Org-unit types).");
        }
    }
}

public sealed class CreateOrgUnitCommandValidator : AbstractValidator<CreateOrgUnitCommand>
{
    public CreateOrgUnitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
    }
}

public sealed class CreateOrgUnitCommandHandler(
    IOrgUnitRepository orgUnits, IReferenceDataRepository referenceData, IAuditRecorder audit, IUnitOfWork unitOfWork)
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

        await OrgUnitTypeRules.EnsureKnownAsync(referenceData, command.UnitType, cancellationToken);

        var orgUnit = OrgUnit.Create(command.Name, command.Code, command.ParentOrgUnitId, command.UnitType);
        orgUnits.Add(orgUnit);
        audit.Record(AuditEventTypes.OrgUnitCreated, AuditTargetTypes.OrgUnit, orgUnit.Id,
            after: new { orgUnit.Name, orgUnit.Code, orgUnit.ParentOrgUnitId, orgUnit.UnitType });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return orgUnit.ToDto();
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
        return orgUnit.ToDto();
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
        return orgUnit.ToDto();
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

        // Deactivating and reactivating are separate, named events, each with the state before and after — a
        // reactivation used to be logged as another "archived" entry and so was invisible in the trail.
        audit.Record(command.Archived ? AuditEventTypes.OrgUnitArchived : AuditEventTypes.OrgUnitRestored,
            AuditTargetTypes.OrgUnit, orgUnit.Id,
            before: new { IsArchived = !command.Archived }, after: new { orgUnit.IsArchived, orgUnit.Name, orgUnit.Code });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return orgUnit.ToDto();
    }
}

// ---- Set head (reporting-line fallback) ----

/// <summary>Designate (or clear) the user who heads an org unit — the reporting-line fallback for its members.</summary>
/// <summary>Sets (or clears) what kind of unit an org unit is.</summary>
public sealed record SetOrgUnitTypeCommand(Guid Id, string? UnitType) : ICommand<OrgUnitDto>;

public sealed class SetOrgUnitTypeCommandHandler(
    IOrgUnitRepository orgUnits, IReferenceDataRepository referenceData, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetOrgUnitTypeCommand, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(SetOrgUnitTypeCommand command, CancellationToken cancellationToken)
    {
        var orgUnit = await orgUnits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("OrgUnit", command.Id);
        await OrgUnitTypeRules.EnsureKnownAsync(referenceData, command.UnitType, cancellationToken);
        var before = orgUnit.UnitType;
        orgUnit.SetUnitType(command.UnitType);
        audit.Record(AuditEventTypes.OrgUnitUpdated, AuditTargetTypes.OrgUnit, orgUnit.Id,
            before: new { UnitType = before }, after: new { orgUnit.UnitType });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return orgUnit.ToDto();
    }
}

public sealed record SetOrgUnitHeadCommand(Guid Id, Guid? HeadUserId) : ICommand<OrgUnitDto>;

public sealed class SetOrgUnitHeadCommandHandler(
    IOrgUnitRepository orgUnits,
    IUserRepository users,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetOrgUnitHeadCommand, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(SetOrgUnitHeadCommand command, CancellationToken cancellationToken)
    {
        var orgUnit = await orgUnits.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("OrgUnit", command.Id);

        if (command.HeadUserId is { } headId && await users.GetByIdAsync(headId, cancellationToken) is null)
        {
            throw new NotFoundException("User", headId);
        }

        orgUnit.SetHead(command.HeadUserId);
        audit.Record(AuditEventTypes.OrgUnitUpdated, AuditTargetTypes.OrgUnit, orgUnit.Id, after: new { orgUnit.HeadUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return orgUnit.ToDto();
    }
}
