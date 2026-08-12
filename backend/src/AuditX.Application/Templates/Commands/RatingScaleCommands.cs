using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Templates.Dtos;
using AuditX.Application.Templates.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Templates;
using FluentValidation;

namespace AuditX.Application.Templates.Commands;

internal static class RatingScaleValidationRules
{
    /// <summary>PointsJson must parse to at least 2 distinctly-valued points, each with a non-empty label and a 0-100 score.</summary>
    public static void EnsureValid(string pointsJson)
    {
        var points = RatingScalePoints.TryParse(pointsJson)
            ?? throw new ConflictException("rating_scale.points_invalid", "Scale points must be a JSON array of {value, label, score}.");

        if (points.Count < 2)
        {
            throw new ConflictException("rating_scale.points_too_few", "A rating scale needs at least two points.");
        }

        if (points.Select(p => p.Value).Distinct().Count() != points.Count)
        {
            throw new ConflictException("rating_scale.points_duplicate_value", "Scale point values must be unique.");
        }

        if (points.Any(p => string.IsNullOrWhiteSpace(p.Label)))
        {
            throw new ConflictException("rating_scale.points_label_required", "Every scale point needs a label.");
        }

        if (points.Any(p => p.Score is < 0 or > 100))
        {
            throw new ConflictException("rating_scale.points_score_range", "Scale point scores must be between 0 and 100.");
        }
    }
}

public sealed record CreateRatingScaleCommand(string Name, string? Description, string PointsJson) : ICommand<RatingScaleDto>;

public sealed class CreateRatingScaleCommandValidator : AbstractValidator<CreateRatingScaleCommand>
{
    public CreateRatingScaleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PointsJson).NotEmpty();
    }
}

public sealed class CreateRatingScaleCommandHandler(IRatingScaleRepository ratingScales, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRatingScaleCommand, RatingScaleDto>
{
    public async Task<RatingScaleDto> Handle(CreateRatingScaleCommand command, CancellationToken cancellationToken)
    {
        if (await ratingScales.GetByNameAsync(command.Name.Trim(), cancellationToken) is not null)
        {
            throw new ConflictException("rating_scale.name_taken", $"A rating scale named '{command.Name}' already exists.");
        }

        RatingScaleValidationRules.EnsureValid(command.PointsJson);

        var scale = RatingScale.Create(command.Name.Trim(), command.Description, command.PointsJson);
        ratingScales.Add(scale);
        audit.Record(AuditEventTypes.RatingScaleConfigured, AuditTargetTypes.RatingScale, scale.Id, after: new { scale.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return scale.ToDto();
    }
}

public sealed record UpdateRatingScaleCommand(Guid Id, string? Name, string? Description, string? PointsJson, bool? IsActive) : ICommand<RatingScaleDto>;

public sealed class UpdateRatingScaleCommandHandler(IRatingScaleRepository ratingScales, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRatingScaleCommand, RatingScaleDto>
{
    public async Task<RatingScaleDto> Handle(UpdateRatingScaleCommand command, CancellationToken cancellationToken)
    {
        var scale = await ratingScales.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Rating scale", command.Id);

        if (command.PointsJson is { } pointsJson)
        {
            RatingScaleValidationRules.EnsureValid(pointsJson);
        }

        scale.Update(command.Name, command.Description, command.PointsJson, command.IsActive);
        audit.Record(AuditEventTypes.RatingScaleConfigured, AuditTargetTypes.RatingScale, scale.Id, after: new { scale.Name, scale.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return scale.ToDto();
    }
}

public sealed record ListRatingScalesQuery(string? Active) : IQuery<IReadOnlyList<RatingScaleDto>>;

public sealed class ListRatingScalesQueryHandler(IRatingScaleRepository ratingScales) : IQueryHandler<ListRatingScalesQuery, IReadOnlyList<RatingScaleDto>>
{
    public async Task<IReadOnlyList<RatingScaleDto>> Handle(ListRatingScalesQuery query, CancellationToken cancellationToken)
    {
        bool? activeOnly = query.Active?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            null or "all" => null,
            _ => throw new ConflictException("invalid_active_filter", $"Unknown active filter '{query.Active}'."),
        };
        var result = await ratingScales.GetAllAsync(activeOnly, cancellationToken);
        return result.Select(s => s.ToDto()).ToArray();
    }
}
