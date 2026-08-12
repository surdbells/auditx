using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Concurrency;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;

namespace AuditX.Application.Templates.Commands;

public sealed record ResponseOptionDto(string Code, string Label, int Order, decimal? Score, bool IsDeficiency, bool IsNotApplicable, bool RequiresComment);

public sealed record ResponseOptionSetDto(string ResponseType, bool IsCustomised, IReadOnlyList<ResponseOptionDto> Options, string? Version);

internal static class ResponseOptionSetSupport
{
    public static ResponseType ParseResponseType(string? value)
        => Enum.TryParse<ResponseType>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var t)
            ? t
            : throw new DomainException("response_option_set.invalid_response_type", $"Unknown response type '{value}'.");

    public static ResponseOptionSetDto ToDto(ResponseType type, IReadOnlyList<ResponseOption> options, string? version, bool isCustomised) => new(
        type.ToSnake(), isCustomised,
        options.Select(o => new ResponseOptionDto(o.Code, o.Label, o.Order, o.Score, o.IsDeficiency, o.IsNotApplicable, o.RequiresComment)).ToArray(),
        version);
}

/// <summary>Replace the option list for a response type (creating the set if the org hasn't customised it yet).</summary>
public sealed record UpdateResponseOptionSetCommand(string ResponseType, string OptionsJson) : ICommand<ResponseOptionSetDto>;

public sealed class UpdateResponseOptionSetCommandHandler(IResponseOptionSetRepository sets, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateResponseOptionSetCommand, ResponseOptionSetDto>
{
    public async Task<ResponseOptionSetDto> Handle(UpdateResponseOptionSetCommand command, CancellationToken cancellationToken)
    {
        var type = ResponseOptionSetSupport.ParseResponseType(command.ResponseType);
        if (!ResponseOptions.SupportsOptionSet(type))
        {
            throw new ConflictException("response_option_set.unsupported_type", "Only verdict-based response types have configurable options.");
        }

        // Validate + normalise (dedupe codes, range-check scores, ensure a conclusive option) before persisting.
        var parsed = ResponseOptions.ParseOrThrow(command.OptionsJson);
        var normalisedJson = AppJson.Serialize(parsed);

        var set = await sets.GetByResponseTypeAsync(type, cancellationToken);
        if (set is null)
        {
            set = ResponseOptionSet.Create(type, normalisedJson);
            sets.Add(set);
        }
        else
        {
            set.UpdateOptions(normalisedJson);
        }

        audit.Record(AuditEventTypes.ResponseOptionSetUpdated, AuditTargetTypes.ResponseOptionSet, set.Id, after: new { responseType = type.ToSnake(), optionCount = parsed.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResponseOptionSetSupport.ToDto(type, parsed, RowVersionToken.Encode(set.Version), isCustomised: !ResponseOptions.MatchesDefaults(type, parsed));
    }
}

/// <summary>Reset a response type's options back to the built-in defaults (deletes the org's customisation).</summary>
public sealed record ResetResponseOptionSetCommand(string ResponseType) : ICommand<ResponseOptionSetDto>;

public sealed class ResetResponseOptionSetCommandHandler(IResponseOptionSetRepository sets, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ResetResponseOptionSetCommand, ResponseOptionSetDto>
{
    public async Task<ResponseOptionSetDto> Handle(ResetResponseOptionSetCommand command, CancellationToken cancellationToken)
    {
        var type = ResponseOptionSetSupport.ParseResponseType(command.ResponseType);
        var set = await sets.GetByResponseTypeAsync(type, cancellationToken);
        if (set is not null)
        {
            set.UpdateOptions(ResponseOptions.DefaultsJson(type));
            audit.Record(AuditEventTypes.ResponseOptionSetUpdated, AuditTargetTypes.ResponseOptionSet, set.Id, after: new { responseType = type.ToSnake(), reset = true });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ResponseOptionSetSupport.ToDto(type, ResponseOptions.Defaults(type), set is null ? null : RowVersionToken.Encode(set.Version), isCustomised: false);
    }
}

public sealed record GetResponseOptionSetQuery(string ResponseType) : IQuery<ResponseOptionSetDto>;

public sealed class GetResponseOptionSetQueryHandler(IResponseOptionSetRepository sets) : IQueryHandler<GetResponseOptionSetQuery, ResponseOptionSetDto>
{
    public async Task<ResponseOptionSetDto> Handle(GetResponseOptionSetQuery query, CancellationToken cancellationToken)
    {
        var type = ResponseOptionSetSupport.ParseResponseType(query.ResponseType);
        var set = await sets.GetByResponseTypeAsync(type, cancellationToken);
        var options = (set is null ? null : ResponseOptions.TryParse(set.OptionsJson)) ?? ResponseOptions.Defaults(type);
        return ResponseOptionSetSupport.ToDto(type, options, set is null ? null : RowVersionToken.Encode(set.Version), isCustomised: !ResponseOptions.MatchesDefaults(type, options));
    }
}

public sealed record ListResponseOptionSetsQuery : IQuery<IReadOnlyList<ResponseOptionSetDto>>;

public sealed class ListResponseOptionSetsQueryHandler(IResponseOptionSetRepository sets) : IQueryHandler<ListResponseOptionSetsQuery, IReadOnlyList<ResponseOptionSetDto>>
{
    private static readonly ResponseType[] Configurable = [ResponseType.PassFailNa, ResponseType.YesNo];

    public async Task<IReadOnlyList<ResponseOptionSetDto>> Handle(ListResponseOptionSetsQuery query, CancellationToken cancellationToken)
    {
        var all = await sets.GetAllAsync(cancellationToken);
        var byType = all.ToDictionary(s => s.ResponseType);
        return Configurable.Select(type =>
        {
            byType.TryGetValue(type, out var set);
            var options = (set is null ? null : ResponseOptions.TryParse(set.OptionsJson)) ?? ResponseOptions.Defaults(type);
            return ResponseOptionSetSupport.ToDto(type, options, set is null ? null : RowVersionToken.Encode(set.Version), isCustomised: !ResponseOptions.MatchesDefaults(type, options));
        }).ToArray();
    }
}
