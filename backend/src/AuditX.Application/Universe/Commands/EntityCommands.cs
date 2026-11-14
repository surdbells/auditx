using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Universe;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Universe.Dtos;
using AuditX.Application.Universe.Mapping;
using AuditX.Application.Universe.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Universe;
using FluentValidation;

namespace AuditX.Application.Universe.Commands;

/// <summary>Who may be made accountable for an entity.</summary>
internal static class EntityOwnerRules
{
    /// <summary>
    /// A newly chosen owner must be a real, active person: not a deactivated account and not a temporary vendor-support
    /// login. An owner left unchanged is not re-checked, so an entity whose owner has since left can still be edited
    /// (and its owner replaced).
    /// </summary>
    public static async Task EnsureAssignableAsync(IUserRepository users, Guid? ownerUserId, Guid? currentOwnerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId is not { } id || id == currentOwnerUserId)
        {
            return;
        }

        var owner = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);
        if (owner.Status == Domain.Enums.UserStatus.Deactivated)
        {
            throw new ConflictException("universe.owner_deactivated",
                $"{owner.DisplayName}'s account is deactivated, so they cannot own an entity. Choose someone else, or reactivate the account first.");
        }

        if (owner.IsSupportAccount)
        {
            throw new ConflictException("universe.owner_support_account",
                "A vendor-support login cannot own an entity. Choose a member of your organisation.");
        }
    }
}

public sealed record CreateEntityCommand(
    string Name, string EntityType, string? Description, Guid? ParentEntityId, Guid? OwnerUserId,
    Guid? OrgUnitId = null, int? ExpectedAuditsPerYear = null)
    : ICommand<EntityDto>;

public sealed class CreateEntityCommandValidator : AbstractValidator<CreateEntityCommand>
{
    public CreateEntityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpectedAuditsPerYear).GreaterThan(0).When(x => x.ExpectedAuditsPerYear is not null);
    }
}

public sealed class CreateEntityCommandHandler(
    IAuditUniverseRepository entities,
    ITaxonomyProvider taxonomy,
    IOrgUnitRepository orgUnits,
    IUserRepository users,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(CreateEntityCommand command, CancellationToken cancellationToken)
    {
        if (!await taxonomy.IsEntityTypeActiveAsync(command.EntityType, cancellationToken))
        {
            throw new ConflictException("universe.unknown_entity_type", $"Entity type '{command.EntityType}' is not in the active taxonomy.");
        }

        if (command.ParentEntityId is { } parentId)
        {
            var parentMap = await entities.GetParentMapAsync(cancellationToken);
            if (!parentMap.ContainsKey(parentId))
            {
                throw new NotFoundException("Parent entity", parentId);
            }
        }

        await EnsureOrgUnitExistsAsync(orgUnits, command.OrgUnitId, cancellationToken);
        await OrgReach.EnsureCoversAsync(currentUser.UserId, Domain.Authorization.PermissionKeys.ManageUniverse, command.OrgUnitId, permissions, cancellationToken);
        await EntityOwnerRules.EnsureAssignableAsync(users, command.OwnerUserId, null, cancellationToken);

        var owner = command.OwnerUserId ?? currentUser.UserId;
        var entity = AuditableEntity.Create(command.EntityType.Trim(), command.Name.Trim(), command.Description, command.ParentEntityId, owner);
        entity.SetOrgUnit(command.OrgUnitId);
        entity.SetExpectedAuditsPerYear(command.ExpectedAuditsPerYear);
        entities.Add(entity);
        audit.Record(AuditEventTypes.EntityCreated, AuditTargetTypes.AuditUniverseEntity, entity.Id,
            after: new { entity.Name, entity.EntityType, entity.ParentEntityId, entity.OwnerUserId, entity.OrgUnitId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    internal static async Task EnsureOrgUnitExistsAsync(IOrgUnitRepository orgUnits, Guid? orgUnitId, CancellationToken cancellationToken)
    {
        if (orgUnitId is { } id && await orgUnits.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Org unit", id);
        }
    }
}

public sealed record UpdateEntityCommand(
    Guid Id, string Name, string EntityType, string? Description, Guid? OwnerUserId, Guid? ParentEntityId, string Version,
    Guid? OrgUnitId = null, int? ExpectedAuditsPerYear = null)
    : ICommand<EntityDto>;

public sealed class UpdateEntityCommandValidator : AbstractValidator<UpdateEntityCommand>
{
    public UpdateEntityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpectedAuditsPerYear).GreaterThan(0).When(x => x.ExpectedAuditsPerYear is not null);
    }
}

public sealed class UpdateEntityCommandHandler(
    IAuditUniverseRepository entities,
    ITaxonomyProvider taxonomy,
    IOrgUnitRepository orgUnits,
    IUserRepository users,
    ICurrentUser currentUser,
    IPermissionResolver permissions,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(UpdateEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);

        if (!string.Equals(RowVersionToken.Encode(entity.Version), command.Version, StringComparison.Ordinal))
        {
            throw new ConflictException("universe.concurrency_conflict", "The entity was modified by someone else; reload and retry.");
        }

        if (!await taxonomy.IsEntityTypeActiveAsync(command.EntityType, cancellationToken))
        {
            throw new ConflictException("universe.unknown_entity_type", $"Entity type '{command.EntityType}' is not in the active taxonomy.");
        }

        await CreateEntityCommandHandler.EnsureOrgUnitExistsAsync(orgUnits, command.OrgUnitId, cancellationToken);
        // Both where it is now and where it is being moved to must be within the caller's reach.
        await OrgReach.EnsureCoversAsync(currentUser.UserId, Domain.Authorization.PermissionKeys.ManageUniverse, entity.OrgUnitId, permissions, cancellationToken);
        await OrgReach.EnsureCoversAsync(currentUser.UserId, Domain.Authorization.PermissionKeys.ManageUniverse, command.OrgUnitId, permissions, cancellationToken);
        await EntityOwnerRules.EnsureAssignableAsync(users, command.OwnerUserId, entity.OwnerUserId, cancellationToken);

        var before = new { entity.Name, entity.EntityType, entity.ParentEntityId, entity.OwnerUserId, entity.OrgUnitId };

        if (command.ParentEntityId != entity.ParentEntityId)
        {
            var parentMap = await entities.GetParentMapAsync(cancellationToken);
            if (command.ParentEntityId is { } pid && !parentMap.ContainsKey(pid))
            {
                throw new NotFoundException("Parent entity", pid);
            }

            if (HierarchyGuard.WouldCreateCycle(parentMap, entity.Id, command.ParentEntityId))
            {
                throw new ConflictException("universe.cycle_detected", "The requested parent would create a cycle.");
            }

            entity.SetParent(command.ParentEntityId);
        }

        entity.UpdateDetails(command.Name.Trim(), command.EntityType.Trim(), command.Description, command.OwnerUserId);
        entity.SetOrgUnit(command.OrgUnitId);
        entity.SetExpectedAuditsPerYear(command.ExpectedAuditsPerYear);
        audit.Record(AuditEventTypes.EntityUpdated, AuditTargetTypes.AuditUniverseEntity, entity.Id,
            before: before, after: new { entity.Name, entity.EntityType, entity.ParentEntityId, entity.OwnerUserId, entity.OrgUnitId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record ArchiveEntityCommand(Guid Id) : ICommand<Unit>;

public sealed class ArchiveEntityCommandHandler(
    IAuditUniverseRepository entities, ICurrentUser currentUser, IPermissionResolver permissions, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveEntityCommand, Unit>
{
    public async Task<Unit> Handle(ArchiveEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);
        await OrgReach.EnsureCoversAsync(currentUser.UserId, Domain.Authorization.PermissionKeys.ManageUniverse, entity.OrgUnitId, permissions, cancellationToken);
        entity.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.EntityArchived, AuditTargetTypes.AuditUniverseEntity, entity.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Bring an archived entity back into the audit universe (the undo of <see cref="ArchiveEntityCommand"/>).</summary>
public sealed record RestoreEntityCommand(Guid Id) : ICommand<EntityDto>;

public sealed class RestoreEntityCommandHandler(
    IAuditUniverseRepository entities, ICurrentUser currentUser, IPermissionResolver permissions, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RestoreEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(RestoreEntityCommand command, CancellationToken cancellationToken)
    {
        // The ordinary lookup hides archived entities — which is exactly the one being asked for here.
        var entity = await entities.GetByIdIncludingArchivedAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);
        await OrgReach.EnsureCoversAsync(currentUser.UserId, Domain.Authorization.PermissionKeys.ManageUniverse, entity.OrgUnitId, permissions, cancellationToken);
        if (!entity.IsDeleted)
        {
            throw new ConflictException("universe.not_archived", "This entity is not archived.");
        }

        var archivedAt = entity.DeletedAt;
        entity.Restore();
        audit.Record(AuditEventTypes.EntityRestored, AuditTargetTypes.AuditUniverseEntity, entity.Id,
            before: new { archived = true, archivedAt }, after: new { archived = false, entity.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record ApplyRiskScoresCommand(
    Guid Id, IReadOnlyDictionary<string, int>? InherentScores, IReadOnlyDictionary<string, int>? ResidualScores, string Version)
    : ICommand<EntityDto>;

public sealed class ApplyRiskScoresCommandHandler(
    IAuditUniverseRepository entities, IRiskDimensionRepository dimensions, ICurrentUser currentUser, IPermissionResolver permissions,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyRiskScoresCommand, EntityDto>
{
    public async Task<EntityDto> Handle(ApplyRiskScoresCommand command, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);
        await OrgReach.EnsureCoversAsync(currentUser.UserId, Domain.Authorization.PermissionKeys.ScoreRisk, entity.OrgUnitId, permissions, cancellationToken);

        if (!string.Equals(RowVersionToken.Encode(entity.Version), command.Version, StringComparison.Ordinal))
        {
            throw new ConflictException("universe.concurrency_conflict", "The entity was modified by someone else; reload and retry.");
        }

        var activeDimensions = (await dimensions.GetAllAsync(activeOnly: true, cancellationToken)).Select(d => d.ToSpec()).ToArray();
        if (activeDimensions.Length == 0)
        {
            throw new ConflictException("universe.no_dimensions", "No active risk dimensions are configured.");
        }

        var before = new { inherent = entity.InherentScores, residual = entity.ResidualScores };
        entity.ApplyRiskScores(command.InherentScores, command.ResidualScores, activeDimensions);
        audit.Record(AuditEventTypes.RiskScoreUpdated, AuditTargetTypes.AuditUniverseEntity, entity.Id,
            before: before,
            after: new { inherent = entity.InherentScores, residual = entity.ResidualScores, entity.CompositeInherentScore, entity.CompositeResidualScore });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}
