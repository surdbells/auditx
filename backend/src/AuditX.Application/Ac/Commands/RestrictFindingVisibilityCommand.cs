using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Ac;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Ac.Commands;

/// <summary>
/// Sets (or updates) the per-finding visibility allow-list (M13, FR-M13-009; CIA). Idempotent on
/// (finding_type, finding_id): re-calling replaces the allow-list. The filter is applied per-requester at read.
/// </summary>
public sealed record RestrictFindingVisibilityCommand(
    string FindingType, Guid FindingId, IReadOnlyList<Guid> AllowedUserIds, string? Reason)
    : ICommand<FindingVisibilityRestrictionDto>;

public sealed class RestrictFindingVisibilityCommandValidator : AbstractValidator<RestrictFindingVisibilityCommand>
{
    public RestrictFindingVisibilityCommandValidator()
    {
        RuleFor(x => x.FindingType).NotEmpty();
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public sealed class RestrictFindingVisibilityCommandHandler(
    IFindingVisibilityRestrictionRepository restrictions, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RestrictFindingVisibilityCommand, FindingVisibilityRestrictionDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FindingVisibilityRestrictionDto> Handle(RestrictFindingVisibilityCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var findingType = ParseFindingType(command.FindingType);
        var allowedJson = JsonSerializer.Serialize(command.AllowedUserIds?.Distinct().ToArray() ?? [], JsonOptions);

        var existing = await restrictions.GetAsync(findingType, command.FindingId, cancellationToken);
        FindingVisibilityRestriction restriction;
        if (existing is null)
        {
            restriction = FindingVisibilityRestriction.Create(findingType, command.FindingId, allowedJson, command.Reason, actorId, clock.UtcNow);
            restrictions.Add(restriction);
        }
        else
        {
            existing.Update(allowedJson, command.Reason, actorId, clock.UtcNow);
            restriction = existing;
        }

        audit.Record(AuditEventTypes.FindingVisibilityRestricted, AuditTargetTypes.FindingVisibilityRestriction, restriction.Id,
            after: new { findingType = findingType.ToString().ToLowerInvariant(), command.FindingId, allowedCount = command.AllowedUserIds?.Count ?? 0 });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return restriction.ToDto();
    }

    internal static FindingType ParseFindingType(string value)
    {
        foreach (var candidate in Enum.GetValues<FindingType>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new DomainException("finding_visibility.invalid_finding_type", "The finding type must be 'exception'.");
    }
}
