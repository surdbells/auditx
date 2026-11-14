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

/// <summary>
/// A response set. <see cref="Id"/> and <see cref="Name"/> are null for a response type's standard set (which exists
/// even before it is customised); a named set has both, and may be retired (<see cref="IsArchived"/>).
/// </summary>
public sealed record ResponseOptionSetDto(
    string ResponseType, bool IsCustomised, IReadOnlyList<ResponseOptionDto> Options, string? Version,
    Guid? Id = null, string? Name = null, bool IsArchived = false);

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

    public static ResponseOptionSetDto ToNamedDto(ResponseOptionSet set)
    {
        var options = ResponseOptions.TryParse(set.OptionsJson) ?? ResponseOptions.Defaults(set.ResponseType);
        return ToDto(set.ResponseType, options, RowVersionToken.Encode(set.Version), isCustomised: true)
            with { Id = set.Id, Name = set.Name, IsArchived = set.IsArchived };
    }

    /// <summary>A named set's name must be unique (case-insensitive) across all named sets.</summary>
    public static async Task EnsureNameFreeAsync(IResponseOptionSetRepository sets, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await sets.FindByNameAsync(name, exceptId, cancellationToken) is not null)
        {
            throw new ConflictException("response_option_set.name_taken", $"A response set named '{name.Trim()}' already exists. Choose another name.");
        }
    }
}

/// <summary>Create a named response set — an additional vocabulary items of a verdict-based type can choose.</summary>
public sealed record CreateResponseSetCommand(string Name, string ResponseType, string OptionsJson) : ICommand<ResponseOptionSetDto>;

public sealed class CreateResponseSetCommandHandler(IResponseOptionSetRepository sets, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateResponseSetCommand, ResponseOptionSetDto>
{
    public async Task<ResponseOptionSetDto> Handle(CreateResponseSetCommand command, CancellationToken cancellationToken)
    {
        var type = ResponseOptionSetSupport.ParseResponseType(command.ResponseType);
        if (!ResponseOptions.SupportsOptionSet(type))
        {
            throw new ConflictException("response_option_set.unsupported_type", "Only verdict-based response types have configurable options.");
        }

        await ResponseOptionSetSupport.EnsureNameFreeAsync(sets, command.Name ?? string.Empty, null, cancellationToken);
        var parsed = ResponseOptions.ParseOrThrow(command.OptionsJson);
        var set = ResponseOptionSet.CreateNamed(type, command.Name ?? string.Empty, AppJson.Serialize(parsed));
        sets.Add(set);
        audit.Record(AuditEventTypes.ResponseOptionSetCreated, AuditTargetTypes.ResponseOptionSet, set.Id,
            after: new { set.Name, responseType = type.ToSnake(), options = parsed.Select(o => o.Label).ToArray() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResponseOptionSetSupport.ToNamedDto(set);
    }
}

/// <summary>Rename a named response set and/or replace its options.</summary>
public sealed record UpdateResponseSetCommand(Guid Id, string Name, string OptionsJson, string? Version) : ICommand<ResponseOptionSetDto>;

public sealed class UpdateResponseSetCommandHandler(IResponseOptionSetRepository sets, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateResponseSetCommand, ResponseOptionSetDto>
{
    public async Task<ResponseOptionSetDto> Handle(UpdateResponseSetCommand command, CancellationToken cancellationToken)
    {
        var set = await sets.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Response set", command.Id);
        if (set.Name is null)
        {
            throw new ConflictException("response_option_set.standard_not_named", "The standard set of a response type is edited by its response type, not by name.");
        }

        if (command.Version is not null && !string.Equals(RowVersionToken.Encode(set.Version), command.Version, StringComparison.Ordinal))
        {
            throw new ConflictException("response_option_set.concurrency_conflict", "This response set was changed by someone else. Reload it and try again.");
        }

        await ResponseOptionSetSupport.EnsureNameFreeAsync(sets, command.Name ?? string.Empty, set.Id, cancellationToken);
        var parsed = ResponseOptions.ParseOrThrow(command.OptionsJson);
        var before = new { set.Name, options = (ResponseOptions.TryParse(set.OptionsJson) ?? []).Select(o => o.Label).ToArray() };
        set.Rename(command.Name ?? string.Empty);
        set.UpdateOptions(AppJson.Serialize(parsed));
        audit.Record(AuditEventTypes.ResponseOptionSetUpdated, AuditTargetTypes.ResponseOptionSet, set.Id,
            before: before, after: new { set.Name, options = parsed.Select(o => o.Label).ToArray() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResponseOptionSetSupport.ToNamedDto(set);
    }
}

/// <summary>Retire a named response set (no longer offered for new items) or bring it back.</summary>
public sealed record SetResponseSetArchivedCommand(Guid Id, bool Archived) : ICommand<ResponseOptionSetDto>;

public sealed class SetResponseSetArchivedCommandHandler(IResponseOptionSetRepository sets, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<SetResponseSetArchivedCommand, ResponseOptionSetDto>
{
    public async Task<ResponseOptionSetDto> Handle(SetResponseSetArchivedCommand command, CancellationToken cancellationToken)
    {
        var set = await sets.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Response set", command.Id);
        var wasArchived = set.IsArchived;
        set.SetArchived(command.Archived);
        audit.Record(command.Archived ? AuditEventTypes.ResponseOptionSetArchived : AuditEventTypes.ResponseOptionSetRestored,
            AuditTargetTypes.ResponseOptionSet, set.Id,
            before: new { IsArchived = wasArchived }, after: new { set.IsArchived, set.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResponseOptionSetSupport.ToNamedDto(set);
    }
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
        var byType = all.Where(s => s.Name is null).ToDictionary(s => s.ResponseType);
        var standard = Configurable.Select(type =>
        {
            byType.TryGetValue(type, out var set);
            var options = (set is null ? null : ResponseOptions.TryParse(set.OptionsJson)) ?? ResponseOptions.Defaults(type);
            return ResponseOptionSetSupport.ToDto(type, options, set is null ? null : RowVersionToken.Encode(set.Version), isCustomised: !ResponseOptions.MatchesDefaults(type, options));
        });

        // Then the named sets, alphabetically.
        var named = all.Where(s => s.Name is not null)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ResponseOptionSetSupport.ToNamedDto);
        return standard.Concat(named).ToArray();
    }
}
