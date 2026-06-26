using AuditX.Application.Abstractions;
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

public sealed record CreateEntityCommand(string Name, string EntityType, string? Description, Guid? ParentEntityId, Guid? OwnerUserId)
    : ICommand<EntityDto>;

public sealed class CreateEntityCommandValidator : AbstractValidator<CreateEntityCommand>
{
    public CreateEntityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateEntityCommandHandler(
    IAuditUniverseRepository entities,
    ITaxonomyProvider taxonomy,
    ICurrentUser currentUser,
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

        var owner = command.OwnerUserId ?? currentUser.UserId;
        var entity = AuditableEntity.Create(command.EntityType.Trim(), command.Name.Trim(), command.Description, command.ParentEntityId, owner);
        entities.Add(entity);
        audit.Record(AuditEventTypes.EntityCreated, AuditTargetTypes.AuditUniverseEntity, entity.Id,
            after: new { entity.Name, entity.EntityType, entity.ParentEntityId, entity.OwnerUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record UpdateEntityCommand(
    Guid Id, string Name, string EntityType, string? Description, Guid? OwnerUserId, Guid? ParentEntityId, string Version)
    : ICommand<EntityDto>;

public sealed class UpdateEntityCommandHandler(
    IAuditUniverseRepository entities,
    ITaxonomyProvider taxonomy,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(UpdateEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);

        if (!string.Equals(Convert.ToBase64String(entity.Version ?? []), command.Version, StringComparison.Ordinal))
        {
            throw new ConflictException("universe.concurrency_conflict", "The entity was modified by someone else; reload and retry.");
        }

        if (!await taxonomy.IsEntityTypeActiveAsync(command.EntityType, cancellationToken))
        {
            throw new ConflictException("universe.unknown_entity_type", $"Entity type '{command.EntityType}' is not in the active taxonomy.");
        }

        var before = new { entity.Name, entity.EntityType, entity.ParentEntityId, entity.OwnerUserId };

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
        audit.Record(AuditEventTypes.EntityUpdated, AuditTargetTypes.AuditUniverseEntity, entity.Id,
            before: before, after: new { entity.Name, entity.EntityType, entity.ParentEntityId, entity.OwnerUserId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record ArchiveEntityCommand(Guid Id) : ICommand<Unit>;

public sealed class ArchiveEntityCommandHandler(
    IAuditUniverseRepository entities, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveEntityCommand, Unit>
{
    public async Task<Unit> Handle(ArchiveEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);
        entity.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.EntityArchived, AuditTargetTypes.AuditUniverseEntity, entity.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ApplyRiskScoresCommand(
    Guid Id, IReadOnlyDictionary<string, int>? InherentScores, IReadOnlyDictionary<string, int>? ResidualScores, string Version)
    : ICommand<EntityDto>;

public sealed class ApplyRiskScoresCommandHandler(
    IAuditUniverseRepository entities, IRiskDimensionRepository dimensions, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyRiskScoresCommand, EntityDto>
{
    public async Task<EntityDto> Handle(ApplyRiskScoresCommand command, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Entity", command.Id);

        if (!string.Equals(Convert.ToBase64String(entity.Version ?? []), command.Version, StringComparison.Ordinal))
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
