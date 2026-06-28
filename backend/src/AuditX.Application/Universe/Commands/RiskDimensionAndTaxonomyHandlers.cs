using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Universe.Dtos;
using AuditX.Application.Universe.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Universe;
using FluentValidation;

namespace AuditX.Application.Universe.Commands;

// ---- Risk dimensions ----

public sealed record CreateRiskDimensionCommand(string Name, decimal Weight, int? ScaleMin, int? ScaleMax, string? ScaleLabelOverridesJson)
    : ICommand<RiskDimensionDto>;

public sealed class CreateRiskDimensionCommandValidator : AbstractValidator<CreateRiskDimensionCommand>
{
    public CreateRiskDimensionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Weight).GreaterThan(0);
    }
}

public sealed class CreateRiskDimensionCommandHandler(IRiskDimensionRepository dimensions, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRiskDimensionCommand, RiskDimensionDto>
{
    public async Task<RiskDimensionDto> Handle(CreateRiskDimensionCommand command, CancellationToken cancellationToken)
    {
        if (await dimensions.GetByNameAsync(command.Name.Trim(), cancellationToken) is not null)
        {
            throw new ConflictException("dimension.name_taken", $"A risk dimension named '{command.Name}' already exists.");
        }

        var dimension = RiskDimension.Create(command.Name.Trim(), command.Weight, command.ScaleMin ?? 1, command.ScaleMax ?? 5, command.ScaleLabelOverridesJson);
        dimensions.Add(dimension);
        audit.Record(AuditEventTypes.RiskDimensionConfigured, AuditTargetTypes.RiskDimension, dimension.Id, after: new { dimension.Name, dimension.Weight });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return dimension.ToDto();
    }
}

public sealed record UpdateRiskDimensionCommand(Guid Id, decimal? Weight, int? ScaleMin, int? ScaleMax, bool? IsActive, string? ScaleLabelOverridesJson)
    : ICommand<RiskDimensionDto>;

public sealed class UpdateRiskDimensionCommandHandler(IRiskDimensionRepository dimensions, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRiskDimensionCommand, RiskDimensionDto>
{
    public async Task<RiskDimensionDto> Handle(UpdateRiskDimensionCommand command, CancellationToken cancellationToken)
    {
        var dimension = await dimensions.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Risk dimension", command.Id);
        dimension.Update(command.Weight, command.ScaleMin, command.ScaleMax, command.IsActive, command.ScaleLabelOverridesJson);
        audit.Record(AuditEventTypes.RiskDimensionConfigured, AuditTargetTypes.RiskDimension, dimension.Id, after: new { dimension.Weight, dimension.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return dimension.ToDto();
    }
}

public sealed record ListRiskDimensionsQuery(string? Active) : IQuery<IReadOnlyList<RiskDimensionDto>>;

public sealed class ListRiskDimensionsQueryHandler(IRiskDimensionRepository dimensions)
    : IQueryHandler<ListRiskDimensionsQuery, IReadOnlyList<RiskDimensionDto>>
{
    public async Task<IReadOnlyList<RiskDimensionDto>> Handle(ListRiskDimensionsQuery query, CancellationToken cancellationToken)
    {
        bool? activeOnly = query.Active?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            null or "all" => null,
            _ => throw new ConflictException("invalid_active_filter", $"Unknown active filter '{query.Active}'."),
        };
        var result = await dimensions.GetAllAsync(activeOnly, cancellationToken);
        return result.Select(d => d.ToDto()).ToArray();
    }
}

// ---- Entity-type taxonomy ----

public sealed record AddEntityTypeCommand(string Type) : ICommand<EntityTypeDto>;

public sealed class AddEntityTypeCommandHandler(IEntityTypeTaxonomyRepository taxonomy, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AddEntityTypeCommand, EntityTypeDto>
{
    public async Task<EntityTypeDto> Handle(AddEntityTypeCommand command, CancellationToken cancellationToken)
    {
        var name = command.Type.Trim();
        if (await taxonomy.GetByNameAsync(name, cancellationToken) is not null)
        {
            throw new ConflictException("taxonomy.type_exists", $"Entity type '{name}' already exists.");
        }

        taxonomy.Add(EntityTypeTaxonomy.Create(name));
        audit.Record(AuditEventTypes.EntityTypeAdded, AuditTargetTypes.EntityTypeTaxonomy, null, payload: new { type = name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new EntityTypeDto(name);
    }
}

/// <summary>The created entity-type, returned so the create response carries a <c>{data}</c> envelope body.</summary>
public sealed record EntityTypeDto(string Type);

public sealed record RemoveEntityTypeCommand(string Type) : ICommand<Unit>;

public sealed class RemoveEntityTypeCommandHandler(
    IEntityTypeTaxonomyRepository taxonomy, IAuditUniverseRepository entities, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveEntityTypeCommand, Unit>
{
    public async Task<Unit> Handle(RemoveEntityTypeCommand command, CancellationToken cancellationToken)
    {
        var name = command.Type.Trim();
        var entry = await taxonomy.GetByNameAsync(name, cancellationToken) ?? throw new NotFoundException("Entity type", name);
        if (await entities.AnyOfTypeAsync(name, cancellationToken))
        {
            throw new ConflictException("taxonomy.type_in_use", $"Entity type '{name}' is in use and cannot be removed.");
        }

        taxonomy.Remove(entry);
        audit.Record(AuditEventTypes.EntityTypeRemoved, AuditTargetTypes.EntityTypeTaxonomy, null, payload: new { type = name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ListEntityTypesQuery : IQuery<IReadOnlyList<string>>;

public sealed class ListEntityTypesQueryHandler(Abstractions.Universe.ITaxonomyProvider taxonomy)
    : IQueryHandler<ListEntityTypesQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(ListEntityTypesQuery query, CancellationToken cancellationToken)
        => taxonomy.GetActiveEntityTypesAsync(cancellationToken);
}
